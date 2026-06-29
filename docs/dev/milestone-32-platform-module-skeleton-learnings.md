# Milestone 32 - Platform & Module Skeleton + Local⇄AWS Deploy Switch — Learning Notes

## Goal

Begin the production-transformation program by laying the **homes and seams** of a modular monolith
without changing any behavior: a `Platform` shared kernel, a `Modules/` placeholder, a
config-driven Local ⇄ AWS deploy switch, the schema-per-module `DbContext` pattern, and an
architecture-test ratchet. No feature changes; CI stays green; no database table moves.

## The big design decision: pattern, not split

The natural instinct for "modularize the monolith" is to start carving the god `SubmissionDbContext`
into per-module contexts immediately. We deliberately did **not**, for one concrete reason:

> The transactional outbox is captured **inside** `SubmissionDbContext.SaveChangesAsync`. That
> single-transaction guarantee (business change + outbox row commit atomically) is the whole point of
> the outbox. Splitting the context now would either break that guarantee or force us to solve
> cross-context transactions prematurely.

So Milestone 32 establishes the **pattern** (a reusable `ModuleDbContext` base, docs, tests) and the
first real carve — the Notifications module with its own `notifications` schema — waits for
Milestone 33. This is the Strangler Fig approach: build the new structure alongside the old, move one
piece at a time, never a big bang.

## What we built

### Two Platform projects (Ports & Adapters made physical)

- `LIAnsureProtect.Platform.Abstractions` — the **ports**. It references **nothing** (enforced by a
  test), which is what makes it a true shared kernel. It holds the relocated domain-event base
  (`IDomainEvent`, `IHasDomainEvents`) plus new primitives: `PlatformProfile`, `PlatformOptions`,
  `IClock`.
- `LIAnsureProtect.Platform` — the **adapters/base infra**. It references only Abstractions and holds
  `ModuleDbContext` (the schema-per-module + outbox-capture template), `SystemClock`,
  `PlatformProfileResolver`, and `AddPlatform(...)`.

Two projects, not one, so the ports↔adapters split is compiler-enforced.

### Why only the domain-event base moved (and not `ICurrentUser`/`IUnitOfWork`)

The plan considered relocating four shared interfaces. We moved only the **domain-event base** in
M32, for a principled reason: the `ModuleDbContext` base and the outbox capture *must* reference the
domain-event base, and Platform may not depend on Domain — so those interfaces **have** to live in
Platform for the base class to exist. `ICurrentUser` and `IUnitOfWork`, by contrast, are not touched
by any M32 Platform code; relocating them would be ~45 files of pure churn with no functional benefit
yet. They move when the first module actually consumes them (M33), keeping this milestone a tight,
low-risk skeleton.

The move is safe: the outbox serializes events by their **simple type name** (`eventType.Name`), so
changing the namespace from `LIAnsureProtect.Domain.Common` to
`LIAnsureProtect.Platform.Abstractions.DomainEvents` does not affect any stored payloads, and the EF
model is unchanged (verified: *"No changes have been made to the model since the last migration."*).

### The Local ⇄ AWS deploy switch

`Platform:Profile` (config, default `Local`) selects the adapter set. `PlatformProfileResolver`
reads it once; `AddPlatform(configuration)` registers shared services; `AddInfrastructure(conn, profile)`
branches adapters. We proved it on **document storage**: `Local` wires `LocalDocumentStorageService`;
`Aws` fails fast with *"AWS document storage adapter arrives in Milestone 42."* A typo
(`Platform:Profile=Azure`) also fails fast. Default-Local means zero behavior change for everyone.

Design choice: `AddInfrastructure`'s `profile` parameter is **defaulted** to `PlatformProfile.Local`
so the existing `DependencyRegistrationTests` (which call it with one argument) keep compiling, while
hosts pass it explicitly.

### The schema-per-module `ModuleDbContext` base

`ModuleDbContext` generalizes exactly what `SubmissionDbContext` does today:

1. `OnModelCreating` applies `HasDefaultSchema(Schema)` when a schema is set.
2. `SaveChangesAsync` collects domain events from tracked aggregates, calls
   `CaptureDomainEventsAsync` (the overridable outbox seam) **before** `base.SaveChangesAsync`, then
   clears the events after a successful save.

To keep M32 behavior-preserving we did **not** reparent `SubmissionDbContext` onto the base (that
could perturb the EF model snapshot). Instead the base is proven by
`ModuleDbContextTests` using a throwaway Sqlite-backed derived context: it asserts the schema is
applied (`Model.GetDefaultSchema()`) and that events are captured then cleared on save.

Because Platform must not depend on Infrastructure (where `OutboxMessage` lives), the base does not
itself create outbox rows — it exposes the `CaptureDomainEventsAsync` hook, and each module's context
overrides it to write its own outbox rows in its own schema. That keeps the dependency direction
clean while still sharing the mechanism.

### Architecture-test ratchet

`ProjectReferenceBoundaryTests` now encodes the new reference direction (including the two Platform
projects) and adds a data-driven `[Fact]` that discovers any `src/Modules/*` project and fails if a
module references another module or a non-allowed project. It passes trivially today (no modules) and
**auto-enforces** the modular-monolith rules the moment the first module lands in M33.

## Gotchas hit during implementation

- **`AddSingleton(enum)` doesn't compile** — `PlatformProfile` is a value type, and
  `AddSingleton<TService>(instance)` requires a reference type. Fixed by registering `PlatformOptions`
  (which carries the profile) instead of the bare enum.
- **xUnit analyzers enforce the 0-warning bar** — `Assert.True(false, msg)` must be `Assert.Fail(msg)`
  (xUnit2020), and `SaveChangesAsync()` in a test wants `TestContext.Current.CancellationToken`
  (xUnit1051). Both fixed to keep `dotnet build` at 0 warnings / 0 errors.

## Verification

- `dotnet build LIAnsureProtect.slnx` — 0 warnings / 0 errors.
- `dotnet test LIAnsureProtect.slnx` — UnitTests 60, IntegrationTests 106 passed (1 PostgreSQL opt-in
  test skipped by default).
- `dotnet ef migrations has-pending-model-changes` — *No changes have been made to the model since the
  last migration* (the "no table moved" guarantee).
- Full local CI (`scripts/run-local-ci.ps1`) before commit; delivered via the protected-`main` PR flow.

## What this unlocks (next milestones)

- **M33** — carve the **Notifications** module into `src/Modules/Notifications/{Domain,Application,
  Infrastructure}` with its own `NotificationsDbContext` (inheriting `ModuleDbContext`) and
  `notifications` schema, and add the deferred **team inbox**. This is where `ICurrentUser`/`IUnitOfWork`
  naturally relocate into the shared kernel.
- **M34+** — carve Underwriting, then Rating/Quoting/Policy, etc., one always-green milestone at a time.
- **M40/M42** — add the first real AWS adapters behind the existing profile switch (SNS/SQS messaging,
  S3 document storage).
