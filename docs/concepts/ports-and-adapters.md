# Ports & Adapters (Hexagonal Architecture)

## The idea in one sentence

Let your application talk to the outside world (databases, storage, messaging, identity) only through
**ports** (interfaces it owns), and provide **adapters** (concrete implementations) that can be
swapped — so the same business code runs against Local equipment today and AWS equipment tomorrow.

## The analogy

Think of the **wall sockets** in a house. Your laptop has a plug (the **port**). The wall gives you a
socket; behind it, the power might come from the grid, a generator, or solar (the **adapters**). Your
laptop neither knows nor cares — it just needs the socket shape to match. Change the power source and
nothing about the laptop changes.

In our app:

- **Port** = `IDocumentStorageService`, `IMessageBus`, `IClock`, `INotificationPublisher` …
- **Adapter (Local)** = `LocalDocumentStorageService` (writes files to disk).
- **Adapter (AWS)** = a future `S3DocumentStorageService` (writes to S3 with presigned URLs).

## The picture

```text
                       ┌──────────────────────────────┐
        HTTP  ───────▶ │  Application / Domain         │
     (driving           │  (business rules)            │
      adapter)          │                              │
                        │   needs ▶ IDocumentStorage ──┼──▶  LocalDocumentStorageService  (Local)
                        │   needs ▶ IMessageBus     ───┼──▶  S3DocumentStorageService     (AWS)
                        └──────────────────────────────┘        (driven adapters)
                              ports owned by the app        adapters chosen at the composition root
```

- **Driving adapters** push requests *in* (the HTTP controllers, the Worker loop).
- **Driven adapters** are things the app calls *out* to (storage, messaging, identity).
- The app depends on the **port interfaces**, never on a specific adapter. The **composition root**
  (`Program.cs`) decides which adapter to register.

## Why we use it

- **The Local ⇄ AWS switch** is only possible because every external concern is a port. We flip one
  config value and a different adapter gets wired. See
  [the deploy switch](deployment-profiles-local-aws-switch.md).
- **Testing** — tests substitute fake adapters for real ones.
- **Incremental cloud adoption** — we add AWS adapters one at a time (S3 in M42, SNS/SQS in M40)
  without rewriting business logic.

## How it shows up in this codebase

- **Ports** live in the dependency-free shared-kernel project
  `src/Platform/LIAnsureProtect.Platform.Abstractions` (and, for now, some Application-owned ports like
  `IDocumentStorageService`). This project **references nothing** — enforced by
  `ProjectReferenceBoundaryTests`.
- **Adapters** live in `src/Platform/LIAnsureProtect.Platform` (e.g. `SystemClock`) and in the
  Infrastructure/module Infrastructure projects (e.g. `LocalDocumentStorageService`).
- **Selection** happens at the composition root: `AddPlatform(configuration)` resolves the profile,
  and `AddInfrastructure(connectionString, profile)` registers the matching adapter — today the
  document-storage port switches Local vs AWS, with AWS failing fast until its adapter ships.

## Relationship to the other concepts

Ports & Adapters is *how* the layers of [Clean Architecture](clean-architecture.md) reach the outside,
and *how* each [Modular Monolith](modular-monolith.md) module stays independent of infrastructure
choices. The `Platform` kernel is the shared home for the most common ports and adapters.
