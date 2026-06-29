# Modules

This folder holds the **bounded-context modules** of the modular monolith. It is intentionally
empty right now — Milestone 32 builds the `Platform` shared kernel and the seams; the first real
module (**Notifications**) is carved out in Milestone 33, and the rest follow one milestone at a
time (Underwriting → M34, Rating/Quoting/Policy → M35, Accounts/Companies/Product Catalog → M36, …).

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
