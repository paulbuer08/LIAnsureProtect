# LIAnsureProtect Encyclopedia — The Living Project Book

This folder is the **living, always-current book** about LIAnsureProtect: what it is, what it is
built from, how it is designed, and — most importantly — **how every workflow actually moves
through the code**, from the click or event that starts it to the row, file, or notification it
produces at the end.

> **Living document rule (mandatory, applies to every milestone):**
> Every milestone that adds, changes, or removes a feature, flow, technology, or pattern **must
> update the affected encyclopedia chapters in the same pull request**. This rule ranks alongside
> the existing continuity rules in `docs/project-status.md` (README, CHANGELOG, project-status,
> learnings). A milestone is not complete while the encyclopedia contradicts the code.

## How to read this book

Every chapter follows the same promise:

- **Simple English first.** Jargon is always explained the first time it appears.
- **Analogies** anchor each big idea to something familiar.
- **Diagrams mirror the code.** Every flow diagram names the *actual* classes and methods, so you
  can put the diagram and the source file side by side and they match.
- **Scenarios** show concrete examples ("Maria the broker submits an application…").

## Chapters

| # | Chapter | What it answers |
|---|---------|-----------------|
| 1 | [The Big Picture](01-the-big-picture.md) | What is this product? Who uses it? What is the end-to-end business story? |
| 2 | [Technology Stack](02-technology-stack.md) | Every technology used, and *why this one and not something else*. |
| 3 | [Architecture](03-architecture.md) | Modular monolith, Clean Architecture, ports & adapters, schema-per-module, the Local⇄AWS switch. |
| 4 | [Design Patterns Catalog](04-design-patterns.md) | Every pattern in the codebase, where it lives, and why it earns its place. |
| 5 | [Flow: Identity & Login](05-flow-identity-and-login.md) | How someone signs up / logs in, and how every API call proves who is calling. |
| 6 | [Flow: Submission Intake](06-flow-submission-intake.md) | A customer/broker applies for cyber insurance — from form click to database row to domain event. |
| 7 | [Flow: Quoting & Rating](07-flow-quoting-and-rating.md) | How a submitted application becomes a priced quote (or an underwriter referral). |
| 8 | [Flow: Underwriting — Referrals, Evidence & AI](08-flow-underwriting.md) | The underwriter workbench: referral operations, evidence requests, document uploads & scanning, the advisory AI review, and the final decision. |
| 9 | [Flow: Quote Acceptance & Policy Binding](09-flow-acceptance-and-binding.md) | The customer says "yes" — how a quote becomes a bound policy. |
| 10 | [Flow: Notifications & Background Processing](10-flow-notifications-and-background.md) | The transactional outbox, the Worker, the dispatcher, consumers, and the personal + team inboxes. |
| 11 | [Observability & Operations](11-observability-and-operations.md) | Correlation IDs, health probes, metrics/traces, idempotency cleanup — how we see and operate the system. |
| 12 | [Flow: Claims (FNOL → Adjudication → Settlement)](12-flow-claims.md) | After a policy is bound, how a cyber-incident claim is filed, assigned to an adjuster, evidenced, reserved, decided, settled, and closed. |

## Where this book fits among the other docs

- `docs/project-status.md` — the *chronological* continuity file (what happened, milestone by milestone).
- `docs/dev/*-learnings.md` — the *reasoning* behind each milestone's decisions.
- `docs/concepts/*` — deep-dives on individual concepts (Clean Architecture, modular monolith…).
- **`docs/encyclopedia/` (this book)** — the *current-state* description of the whole system.
  History lives elsewhere; this book always describes **now**.
