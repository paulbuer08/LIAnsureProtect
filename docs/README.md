# Documentation Map — What To Read For Which Question

> **Living document.** 100+ files live under `docs/`, but you only ever need a handful — the rest
> is the historical archive. Start at Tier 1; drop to lower tiers only when you need them.

## Tier 1 — Start here (the five documents that answer 95% of questions)

| Question | Read this |
|---|---|
| *How does the system work today?* (architecture, patterns, every workflow with diagrams) | [**The Encyclopedia**](encyclopedia/README.md) — 12 chapters, the living project book |
| *How did we get here?* (every milestone, the six eras, Strangler Fig story) | [**The Build History**](build-history.md) |
| *How do I run the app?* (setup, Auth0, three-terminal run, LocalStack/Redis) | [**Running The App**](guides/running-the-app.md) |
| *How do I test the UI as each role?* (personas, scenarios, boundary checks) | [**Manual Testing Guide**](guides/manual-testing-guide.md) |
| *Where are we and what's next?* | [**Project Status**](project-status.md) + [**Roadmap**](dev/production-transformation-roadmap.md) |

## Tier 2 — Living references (read when the topic comes up)

| Topic | Document |
|---|---|
| Async/await + eventing conventions (global best practice) | [dev/async-and-eventing-conventions.md](dev/async-and-eventing-conventions.md) |
| CI/CD flow and branch protection | [dev/ci-cd-flow.md](dev/ci-cd-flow.md) |
| GitHub repo, automation, CodeQL/rulesets | [dev/github-repository-and-automation.md](dev/github-repository-and-automation.md) |
| Package/tool version management | [dev/dependency-management.md](dev/dependency-management.md) |
| Per-milestone documentation rules | [dev/milestone-documentation-practice.md](dev/milestone-documentation-practice.md) |
| Business domain: cyber specialty insurance | [business/cyber-specialty-insurance-overview.md](business/cyber-specialty-insurance-overview.md) |
| Business domain: user roles & ownership rules | [business/user-roles.md](business/user-roles.md) |
| Core concepts explained (modular monolith, outbox, …) | [concepts/README.md](concepts/README.md) |
| Architecture overview + decision records (ADRs) | [architecture/overview.md](architecture/overview.md) · [architecture/decision-records/](architecture/decision-records) |

## Tier 3 — The historical archive (rarely needed; kept as the audit trail)

These are point-in-time records. They are **not** kept up to date — the Tier-1 living documents
supersede them — but they preserve the full reasoning behind every step and are linked from the
Build History.

- **`dev/milestone-*.md`** — every milestone's design/learnings/handoff record (M2 → today),
  named by milestone number.
- **`dev/post-m44-deep-audit.md`**, **`dev/referral-queue-hardening-spec.md`** — audit reports
  and implemented specs.
- **`superpowers/plans/`** — execution plans for the plan-driven milestones.
- **`dev/run-the-app.md`** — the Milestone-9-era run guide (banner-marked historical; still holds
  the troubleshooting catalog and the manual Auth0-token appendix).
- **`dev/pattern-roadmap-after-milestone-11.md`** — an early pattern audit (banner-marked
  historical; superseded by the Encyclopedia's design-patterns chapter and the Roadmap).

## The rule that keeps this manageable

Each milestone PR may **add one design/learnings record to the archive** and must **update the
Tier-1 living documents** (Encyclopedia chapters it touches, Build History era table, Project
Status, and the guides when behavior changes). New standalone reference docs need a reason a
Tier-1 document can't hold the content.
