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
M33  First real carve: Notifications module gets its own DbContext + `notifications` schema,
     inheriting ModuleDbContext (its own outbox lives in its schema).
M34+ Carve the remaining contexts one per milestone, each always-green.
```

## Design-time factory (per-module migrations)

Each module needs EF Core to find its `DbContext` at design time (for `dotnet ef migrations add`).
The standard template — added per module when it is created — is:

```csharp
// In the module's Infrastructure project, next to its DbContext.
public sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql("Host=localhost;Database=liansureprotect;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"))
            .Options;
        return new NotificationsDbContext(options);
    }
}
```

Note `MigrationsHistoryTable(..., "notifications")` — each module tracks its own migration history
inside its own schema, so migrations stay independent.

## Why we use it

- **True data isolation** — a module physically cannot read or corrupt another's tables.
- **Independent evolution** — each module's migrations move at their own pace.
- **A clean path to microservices** — a schema can later become its own database with minimal change.

## How it shows up in this codebase (Milestone 32)

- `ModuleDbContext` base + the schema/outbox capture: `src/Platform/LIAnsureProtect.Platform/Persistence/ModuleDbContext.cs`.
- Proven by `tests/LIAnsureProtect.IntegrationTests/Platform/ModuleDbContextTests.cs` (schema applied,
  events captured then cleared) — without touching any existing table.
