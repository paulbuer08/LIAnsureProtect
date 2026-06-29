# Milestone 33 - Notifications Module (first context carve) — Learning Notes

## Goal

Perform the **first real bounded-context carve** of the modular-monolith program: lift the
Notifications context out of the legacy layered projects into
`src/Modules/Notifications/{Domain,Application,Infrastructure}`, give it its own
`NotificationsDbContext` owning a dedicated `notifications` PostgreSQL schema, and rewire the outbox
dispatcher to feed it through a module port. **Behavior-preserving** — no feature changes. The team
inbox (a new feature) is a separate, following milestone.

## What moved where

| Concern | Before (legacy) | After (module) |
|---|---|---|
| Inbox aggregate | `Infrastructure/Persistence/Notifications/NotificationInboxEntry` | `Modules.Notifications.Domain` |
| Contracts (`NotificationMessage`, audiences, types, titles, publish result/port, inbox repo port) | `Application/Notifications` | `Modules.Notifications.Application` |
| Read side (`ListMyNotifications`, `MarkNotificationRead`) | `Application/Notifications` | `Modules.Notifications.Application` |
| Inbox EF (`DbContext`, config, repository) + publisher | `Infrastructure` | `Modules.Notifications.Infrastructure` |
| Inbox table | `public.notification_inbox_entries` | `notifications.notification_inbox_entries` |

The dispatcher (`OutboxDispatcher`) and the mapper (`OutboxNotificationMapper`) **stayed** in legacy
Infrastructure because they deserialize other contexts' domain events (Quotes/Policies). True
decoupling needs integration-event contracts, scheduled for M40. Legacy Infrastructure now references
the Notifications module's Application contracts — an acceptable strangler consumer seam (modules
still may not reference each other or legacy).

## The two hard problems and how we solved them

### 1. Cross-context handoff without a distributed transaction

The dispatcher reads the outbox (`SubmissionDbContext`) and writes the inbox
(`NotificationsDbContext`) — two contexts. Instead of a shared transaction, we use **idempotent
ordered projection**:

```
project to inbox (NotificationsDbContext, idempotent on source_outbox_message_id, own save)
  → publish
  → mark the outbox row processed (SubmissionDbContext save)
```

A crash anywhere just re-delivers; the unique index on `source_outbox_message_id` dedupes the inbox
entry. This is at-least-once — observably identical to the old single-context behavior — and is the
pattern every future module handoff will reuse. The new inbound port is `INotificationProjector`
(implemented by `NotificationInboxProjector`).

### 2. The Domain aggregate must not depend on Application

The old `NotificationInboxEntry.FromNotificationMessage(NotificationMessage, …)` factory coupled the
entity to an Application DTO. In the module, the entity lives in Domain, so we replaced that with a
primitive `NotificationInboxEntry.Create(recipientUserId, audience, type, …)` factory and moved the
`NotificationMessage` → entity mapping into the projector (Infrastructure). Domain now depends only
on the Platform shared kernel.

## Schema move: drop-and-recreate (not `SET SCHEMA`)

Moving the table from `public` to `notifications` could be done with
`ALTER TABLE … SET SCHEMA notifications` (preserving data). We chose **drop-and-recreate** because:

- There is **no production deployment** (local/dev only), so there is no data to preserve.
- The inbox is a **rebuildable read model** (re-projected from the outbox).
- It avoids fragile hand-authored cross-context migrations.

So `SubmissionDbContext` got a `DropNotificationInbox` migration and `NotificationsDbContext` got a
`CreateNotificationsSchema` migration. With production data we would use `SET SCHEMA` instead — see
`docs/concepts/schema-per-module.md`.

## Two DbContexts → two migration histories (the riskiest plumbing)

Each context owns its own migrations and `__EFMigrationsHistory` (the module's lives in its
`notifications` schema via `MigrationsHistoryTable(..., "notifications")`). Every `dotnet ef` command
now needs `--context`. We updated the dev scripts (`update-database.ps1`, the
`Assert-SubmissionMigrationsExist` guard in `common.ps1`) and `.github/workflows/ci.yml` to apply
**both** contexts. The contexts touch different tables (public vs notifications schema), so apply
order doesn't matter.

We did **not** add an `IDesignTimeDbContextFactory` — EF builds the context from the Api host's DI
registration (`AddNotificationsModule`), which reads the correct per-environment connection string,
exactly like `SubmissionDbContext`.

## ICurrentUser relocated to the shared kernel

`ICurrentUser` moved from `Application/Common/Security` to
`Platform.Abstractions.Security` because the module's handlers need it and a module may reference only
`Platform.Abstractions`. `ApplicationRoles`/`ApplicationPolicies` stayed in legacy (app-wide security
names). `IUnitOfWork` stayed in legacy — the Notifications module saves via its own context; it will
relocate when a carved *write* module needs it (same minimal-churn principle as M32).

## Architecture-test ratchet did its job

The module-boundary `[Fact]` added in M32 auto-validated the three new module projects with no new
code. We only updated the explicit `Infrastructure`/`Api`/`Worker` reference rows for the new
consumer seam.

## Verification

- `dotnet build LIAnsureProtect.slnx` — 0 warnings / 0 errors.
- `dotnet test` — UnitTests 60, IntegrationTests 107 (incl. the rewired dispatcher tests against two
  Sqlite contexts and the new `NotificationsDbContext` migration test), 1 PostgreSQL opt-in skipped.
- `dotnet ef migrations has-pending-model-changes` — clean for **both** contexts.
- Full local CI applies both contexts' migrations against the fresh Docker Postgres.

## What's next

- **M34 — Team inbox**: persist `underwriting-operations`/`binding-operations` notifications with
  per-user read receipts (created lazily on mark-read), role→audience visibility in the list query,
  and the frontend to surface team notifications. This is where the second module-owned table and a
  read-receipt aggregate join the Notifications module.
- Later (M40): replace the legacy dispatcher's domain-event coupling with integration-event contracts
  so producing contexts publish stable events and Notifications subscribes.
