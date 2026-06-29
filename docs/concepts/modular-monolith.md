# Modular Monolith

## The idea in one sentence

Build **one** application you deploy as a single unit, but **slice its insides** into independent
**bounded-context modules** — each with its own code, its own database schema, and a public contract
— so it has the clean boundaries of microservices without the operational pain.

## The analogy

A modular monolith is **one office building** (you lock one front door, run one set of utilities),
but inside it there are **separate departments** on separate floors. Underwriting doesn't walk into
Claims' filing room and grab folders; it sends a **memo** (an event or a contract call). You could,
years later, move a department into its own building (a microservice) — but only if and when the
extra cost is worth it.

## Monolith vs. modular monolith vs. microservices

```text
Big-ball-of-mud monolith     Modular monolith                Microservices
┌───────────────────┐        ┌───────────────────┐          ┌─────┐ ┌─────┐ ┌─────┐
│ everything tangled│        │ Subm. │ U/W │ ...  │          │Subm.│ │ U/W │ │ ... │
│ shared tables     │        │ each own schema    │          │ own │ │ own │ │ own │
│ one deploy        │        │ talk via contracts │          │ DB  │ │ DB  │ │ DB  │
└───────────────────┘        │ ONE deploy         │          └─────┘ └─────┘ └─────┘
                             └───────────────────┘          many deploys, network calls
   easy to write,             clean boundaries,               independent scaling,
   impossible to change       one deploy to run               heavy ops + distributed bugs
```

We pick the **middle**: clean boundaries now, the option to extract a service later (the
"Strangler Fig" approach), without paying for distributed systems before we need to.

## The rules that make it "modular" (not just a monolith)

1. **Each module is its own Clean-Architecture slice** — `Domain / Application / Infrastructure`
   (see [Clean Architecture](clean-architecture.md)), as **separate projects** so the compiler
   enforces the boundaries.
2. **Each module owns its own database schema** — never another module's tables. See
   [Schema-per-Module](schema-per-module.md).
3. **Modules never reference each other's internals.** Cross-context links are by **id + integration
   event**, not a foreign key or a direct method call into another module's domain.
4. **Shared plumbing lives in the `Platform` kernel** (outbox, idempotency, domain-event base,
   messaging, tenancy, audit) and is reached through [Ports & Adapters](ports-and-adapters.md).

## Our target contexts (Cyber-only for now)

`Platform` (shared kernel) · `Identity & Access` · `Accounts/Companies` · `Product Catalog` ·
`Submissions/Intake` · `Rating` · `Underwriting` · `Quoting` · `Policy` · `Claims` · `Documents` ·
`Notifications`.

## Why we use it

- **Change safety** — a change in Underwriting can't silently break Rating; the boundary is real.
- **Learnability** — the codebase reads like the business (departments you can name).
- **Future-proofing** — any module can graduate to a microservice later with minimal rework, because
  it already talks through contracts and owns its data.

## How it shows up in this codebase

- `src/Platform/` holds the shared kernel; `src/Modules/<Context>/` will hold each module (the first,
  **Notifications**, is carved in Milestone 33).
- The boundary rules are **enforced by tests** in `ProjectReferenceBoundaryTests.cs`, including a
  ratchet that automatically checks any future `src/Modules/*` project.
- Today the legacy `src/LIAnsureProtect.{Domain,Application,Infrastructure}` projects still hold most
  contexts; they are **strangled** into modules one milestone at a time, never in a big bang.
