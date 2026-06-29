# Modules

This folder holds the **bounded-context modules** of the modular monolith. The first real module,
**Notifications** (`Notifications/`), was carved out in Milestone 33 — it owns the notification inbox
in its own `notifications` PostgreSQL schema. The rest follow one milestone at a time (team inbox →
M34, Underwriting, Rating/Quoting/Policy, Accounts/Companies/Product Catalog, …). Milestone 32 built
the `Platform` shared kernel and the seams these modules plug into.

## What a module looks like

Each module is a small Clean-Architecture slice made of **three projects**, so its boundaries are
enforced by the compiler (and by `ProjectReferenceBoundaryTests`):

```
src/Modules/<Context>/
├─ LIAnsureProtect.Modules.<Context>.Domain          ← entities, value objects, rules, domain events
├─ LIAnsureProtect.Modules.<Context>.Application     ← use cases (commands/queries), the ports it needs
└─ LIAnsureProtect.Modules.<Context>.Infrastructure  ← its own DbContext (its own DB schema), adapters
```

## The rules (enforced by architecture tests)

1. A module's **Domain** references only `LIAnsureProtect.Platform.Abstractions` (the shared kernel ports).
2. A module's **Application** references its own Domain + `Platform.Abstractions`.
3. A module's **Infrastructure** references its own Application/Domain + `Platform` / `Platform.Abstractions`.
4. **No module references another module.** Cross-context links are by **id + integration event**,
   never a foreign key into another module's schema.
5. Each module **owns its own PostgreSQL schema** via a `DbContext` that inherits
   `LIAnsureProtect.Platform.Persistence.ModuleDbContext` (which applies `HasDefaultSchema` and the
   transactional domain-event/outbox capture).

See `docs/concepts/` for the full explanations (Clean Architecture, Modular Monolith,
Ports & Adapters, schema-per-module, and the Local ⇄ AWS deploy switch).
