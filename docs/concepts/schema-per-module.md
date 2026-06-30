# Schema-per-Module

## The idea in one sentence

Every bounded-context module keeps its tables in **its own PostgreSQL schema** (a named namespace
inside the one database) and reaches **only** its own schema — so modules share a database server but
never share tables.

## The analogy

One **building**, many **filing rooms**. All departments are under one roof (one database), but each
has a locked filing room (a schema: `notifications`, `underwriting`, …). Underwriting can't open
Claims' cabinets. If they need something, they ask via a **memo** (an id reference + an event), not by
walking in and pulling a folder.

## What a "schema" is here

PostgreSQL groups tables into **schemas**. The default is `public`. Schema-per-module means:

```text
liansureprotect database
├─ public           (legacy tables, still here in M32 — strangled out over time)
├─ notifications    notifications.inbox_entries, notifications.outbox_messages, ...
├─ underwriting     underwriting.referrals, underwriting.evidence_requests, ...
└─ rating           rating.provider_attempts, ...
```

- Each module has **its own `DbContext`** mapped to its schema (`HasDefaultSchema("notifications")`).
- Each module has **its own EF Core migrations** and its own `__EFMigrationsHistory` row-space.
- **No cross-schema foreign keys.** A reference to another context is stored as a plain id; the link
  is maintained by events, not database constraints.

## The reusable base class

`LIAnsureProtect.Platform.Persistence.ModuleDbContext` is the template every module context inherits.
It bakes in two behaviors so each module gets them for free:

1. **Schema** — applies `HasDefaultSchema(Schema)` in `OnModelCreating`.
2. **Transactional domain-event capture** — on `SaveChangesAsync`, it collects domain events from
   tracked aggregates, calls `CaptureDomainEventsAsync` (where the module writes outbox rows) **before**
   `base.SaveChangesAsync`, then clears the events after a successful save.

## The constraint that shapes the rollout: the transactional outbox

The whole point of the **transactional outbox** is that a business change and the event recording it
**commit in the same database transaction** — so we can never publish an event for a change that
didn't save, or save a change whose event got lost.

In today's code that capture happens *inside* `SubmissionDbContext.SaveChangesAsync`. That is exactly
why we **do not** split the god `SubmissionDbContext` or move its tables in Milestone 32: doing so
would either break the single-transaction guarantee or force us to solve cross-context transactions
prematurely — the opposite of a safe, behavior-preserving step.

So the rollout is:

```text
M32  Establish the PATTERN: ModuleDbContext base + this doc + tests. No tables move.
M33  First real carve (DONE): the Notifications module gets its own NotificationsDbContext +
     `notifications` schema, inheriting ModuleDbContext.
M35  Underwriting carve begins (DONE): the AI review slice gets the UnderwritingDbContext +
     `underwriting` schema, reading quote context through a cross-context port (no FK to quotes).
M36+ Carve the remaining contexts/slices one per milestone, each always-green.
```

### Moving an existing table to its schema: `SET SCHEMA` vs. drop-and-recreate

When a carve relocates a table that already exists in `public`, there are two ways:

- **`ALTER TABLE public.x SET SCHEMA newschema`** — preserves data. Use this once there is production
  data to keep.
- **Drop-and-recreate** — the old context drops the table; the new context creates it in its schema.
  Simpler and avoids fragile hand-authored cross-context migrations, but loses existing rows.

M33 used **drop-and-recreate** for `notification_inbox_entries` because there is no production
deployment yet and the inbox is a rebuildable read model: `SubmissionDbContext` got a
`DropNotificationInbox` migration and `NotificationsDbContext` a `CreateNotificationsSchema` migration.

## Per-module migrations (two DbContexts, two histories)

Each module owns its own migrations and its own `__EFMigrationsHistory` (the Notifications module's
lives in its `notifications` schema via `MigrationsHistoryTable(..., "notifications")` in
`AddNotificationsModule`). Because there are now multiple `DbContext`s, every `dotnet ef` command
must pass `--context`, and the dev scripts + CI apply **each** context's migrations:

```text
dotnet ef database update --context SubmissionDbContext   --project src/LIAnsureProtect.Infrastructure ...
dotnet ef database update --context NotificationsDbContext --project src/Modules/Notifications/...Infrastructure ...
```

EF finds each context at design time from the **Api host's DI registration** (`AddNotificationsModule`),
which supplies the correct per-environment connection string — so no `IDesignTimeDbContextFactory` is
needed.

## Why we use it

- **True data isolation** — a module physically cannot read or corrupt another's tables.
- **Independent evolution** — each module's migrations move at their own pace.
- **A clean path to microservices** — a schema can later become its own database with minimal change.

## How it shows up in this codebase (Milestone 32)

- `ModuleDbContext` base + the schema/outbox capture: `src/Platform/LIAnsureProtect.Platform/Persistence/ModuleDbContext.cs`.
- Proven by `tests/LIAnsureProtect.IntegrationTests/Platform/ModuleDbContextTests.cs` (schema applied,
  events captured then cleared) — without touching any existing table.
