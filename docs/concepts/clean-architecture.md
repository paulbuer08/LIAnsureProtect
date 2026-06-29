# Clean Architecture

## The idea in one sentence

Arrange code in layers so the **business rules** sit in the middle and never depend on the
**outside details** (databases, web frameworks, cloud SDKs) — the details depend on the rules, not
the other way around.

## The analogy

Think of a **restaurant**:

- The **recipe and the chef's judgement** (business rules) are the heart. They don't care whether
  orders arrive by phone, app, or a waiter, or whether ingredients come from this supplier or that.
- The **waiters and the order screen** (the web API) just take requests in and bring plates out.
- The **pantry and suppliers** (the database, S3, message queues) just store and fetch things.

If you swap the order screen or change suppliers, the recipes don't change. That independence is the
whole point.

## The layers (and the dependency direction)

```text
        ┌─────────────────────────────────────────────┐
        │  Host edge (Api / Worker)                    │  HTTP, DI wiring, auth middleware
        │   depends on ▼                               │
        │  Infrastructure                              │  EF Core, Postgres, files, HTTP clients
        │   depends on ▼                               │
        │  Application                                 │  use cases (commands/queries), ports
        │   depends on ▼                               │
        │  Domain                                      │  entities, value objects, rules, events
        └─────────────────────────────────────────────┘
                 arrows point INWARD only
```

- **Domain** — the business model: `Submission`, `Quote`, `Policy`, their rules and domain events.
  Depends on nothing except the shared-kernel primitives (see [Ports & Adapters](ports-and-adapters.md)).
- **Application** — the use cases: `CreateSubmissionCommand`, `ListMyNotificationsQuery`, plus the
  **ports** (interfaces) those use cases need, e.g. `ISubmissionRepository`, `IDocumentStorageService`.
- **Infrastructure** — the **adapters**: EF Core repositories, the Postgres `DbContext`, the local
  filesystem document store. It *implements* the Application's ports.
- **Host edge** — `Api` and `Worker`: translate HTTP/host concerns and **wire everything together**
  (the composition root).

The golden rule: **dependencies only point inward.** Domain never references Infrastructure.

## Why we use it

- **Testability** — use cases are tested with fakes/mocks for the ports, no database required.
- **Swappability** — change Postgres details, or filesystem → S3, without touching business rules.
- **Clarity** — each file has an obvious home; new contributors learn the shape fast.

## How it shows up in this codebase

- The request flow `POST /api/v1/submissions` runs
  `SubmissionsController → CreateSubmissionCommand → ValidationBehavior → handler → ISubmissionRepository → EfCoreSubmissionRepository → IUnitOfWork`.
- The dependency direction is **enforced by a test**:
  `tests/LIAnsureProtect.UnitTests/Architecture/ProjectReferenceBoundaryTests.cs` reads each
  project's `.csproj` and fails the build if a project references something it shouldn't.

## How it relates to the bigger picture

Clean Architecture is the layering *inside* one unit. In our [Modular Monolith](modular-monolith.md),
**each bounded-context module repeats this same layering** (`Domain / Application / Infrastructure`),
and the shared primitives live in the `Platform` kernel via [Ports & Adapters](ports-and-adapters.md).
