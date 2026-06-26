# Milestone 31 - Notification Inbox Read Model Foundation Learnings

## What This Milestone Added

Milestones 21, 26, and 30 built the notification **sending** side: domain events become outbox rows, the Worker dispatcher maps them to `NotificationMessage`s and hands them to a local publisher. But nothing kept them — no user could open the app and read what happened. Milestone 31 adds the **receiving** half: a per-recipient inbox read model users can list and mark read.

```text
Analogy:
  The dispatcher is the mailroom clerk. Before M31 it stamped letters and handed
  them to a courier, then forgot them. Now it also drops a copy in the recipient's
  mailbox so they can read it later.
```

## The Flow

```text
outbox_messages (pending)
  -> OutboxDispatcher maps the event to a NotificationMessage   (unchanged)
  -> if Audience == "customer-or-broker":
       add a notification_inbox_entries row (idempotent on source_outbox_message_id)
  -> publish + mark processed                                   (unchanged)
  -> SubmissionDbContext.SaveChangesAsync(...)   <- inbox row commits with outbox state

GET  /api/v1/notifications              -> owner-scoped list (newest 50) + unread count
POST /api/v1/notifications/{id}/read    -> owner-scoped, idempotent mark-as-read
```

## Key Decisions

- **PostgreSQL-first, behind an Application interface.** The architecture *plans* DynamoDB for the inbox, but we used the project's consistent "simplest durable store first, swap behind the interface later" pattern (same as `IDocumentStorageService` local-before-S3). `INotificationInboxRepository` lives in Application and returns Application DTOs; the EF implementation and the `NotificationInboxEntry` entity live in Infrastructure (like `OutboxMessage`). A DynamoDB implementation can replace `EfNotificationInboxRepository` later with no Application change.

- **Personal inbox only (audience scoping).** Of the 11 notification types, 8 are addressed to a person (`customer-or-broker`) and 3 to a team (`underwriting-operations`, `binding-operations`). Only person-addressed messages get an inbox row, because a team inbox needs a different model (fan-out to all underwriters, or role-scoped queries). Filtering on `Audience == customer-or-broker` keeps the slice small and correct. Team inbox is a future milestone.

- **The write lives in the dispatcher, not on the Application interface.** Creating inbox rows is an Infrastructure concern triggered by outbox processing, so the dispatcher writes directly to the `DbContext` it already owns — keeping the inbox row in the *same* `SaveChangesAsync` as the outbox state change. The Application `INotificationInboxRepository` therefore exposes only read operations (list, unread count, mark-read), so Application never references an Infrastructure entity.

- **Idempotent inbox writes.** A publish failure leaves the outbox row pending, so the dispatcher reprocesses it. To avoid duplicate inbox rows, the dispatcher checks for an existing entry by `source_outbox_message_id` before adding, backed by a unique index on that column.

- **Owner-scoped reads.** Every read/mutation filters by `recipient_user_id == ICurrentUser.UserId` (the Milestone 11 ownership pattern), and the controller sits behind a new `Notifications.Read` policy (Customer/Broker/Admin). Marking another user's notification returns 404.

- **Self-describing API.** A friendly `title` is computed from the notification type in the Application layer (`NotificationInboxTitles`), so the React layer stays dumb and just renders.

## Why No Bell-With-Badge On The Dashboard (Yet)

The dashboard test renders the page without a TanStack Query provider. A live unread-count bell there would require a `QueryClient` in that test and risk breaking it, so the dashboard gets a plain link and the unread count is shown on the notifications page itself. A dashboard bell-with-badge is a small future enhancement.

## Out Of Scope (Deferred)

- Team inbox for `underwriting-operations` / `binding-operations` audiences.
- DynamoDB read-model implementation.
- Production email/SMS delivery, notification preference center, real-time push/SignalR, per-type templates, messaging threads.
- Pagination of the inbox (currently newest 50).

## Verification

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
npm --prefix src/LIAnsureProtect.Web run build
npm --prefix src/LIAnsureProtect.Web run lint
npm --prefix src/LIAnsureProtect.Web run test
```

Results: build 0/0; UnitTests 57 passed, IntegrationTests 96 passed with 1 PostgreSQL opt-in test skipped; EF pending-model check reported no changes after the migration; frontend build + lint clean and vitest 34 passed across 9 files. This milestone was delivered through the protected-`main` pull-request flow, so GitHub Actions CI also gated the merge.

## What To Remember

The notification system now has three layers: `outbox_messages` (durable work), the dispatcher + publisher (sending), and `notification_inbox_entries` (the per-user read model). When adding a new person-addressed notification type, it flows into the inbox automatically — just add a friendly title in `NotificationInboxTitles`.
