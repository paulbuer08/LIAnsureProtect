# Concepts Handbook

Plain-English explanations of the architecture ideas behind LIAnsureProtect. Each page uses simple
language, a diagram, and an analogy, then says *why* we use it and *how* it shows up in this codebase.

These pages are an **acceptance criterion** of the production-transformation program: every concept,
tool, AWS service, and pattern we adopt gets documented richly here (or under the matching `docs/`
folder) so the reasoning is never lost.

## Pages

| Page | One-line idea |
|---|---|
| [Clean Architecture](clean-architecture.md) | Layer code so business rules don't depend on databases or frameworks. |
| [Modular Monolith](modular-monolith.md) | One deployable app split into independent bounded-context modules. |
| [Ports & Adapters (Hexagonal)](ports-and-adapters.md) | Talk to the outside world through interfaces you can swap. |
| [Local ⇄ AWS Deploy Switch](deployment-profiles-local-aws-switch.md) | One config value picks Local or AWS adapters; same image everywhere. |
| [Schema-per-Module](schema-per-module.md) | Each module owns its own database schema; no shared tables. |

## How they fit together

```text
Clean Architecture  →  layering INSIDE one thing (Domain → Application → Infrastructure → host)
Modular Monolith    →  many such things (bounded contexts) in ONE deployable, each its own schema
Ports & Adapters    →  HOW modules reach storage/messaging/identity: interfaces with swappable adapters
Deploy switch       →  WHICH adapters get plugged in (Local vs AWS), chosen by Platform:Profile
Schema-per-module   →  HOW each module's data stays private inside the one shared database
```

> Building analogy: the app is one **office building** (single deployable). Each **floor** is a
> department/bounded context with its own **filing room** (schema). Departments exchange **memos**
> (contracts/events), never rummaging in each other's cabinets. The **plumbing and power** (storage,
> messaging, identity) are standardized **wall sockets** (ports) you plug Local or AWS equipment
> (adapters) into.
