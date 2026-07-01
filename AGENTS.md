# AGENTS.md — Operating rules for any coding agent on LIAnsureProtect

This file tells an automated coding agent (Codex, Claude, etc.) how to work in this repository so its
output matches the project's established standards. Read it fully before making changes. It is the
agent-neutral companion to `docs/project-status.md` (the human-facing continuity checkpoint).

## 0. Read these first (continuity)

1. `docs/project-status.md` — source of truth for where the project is, the current milestone, and the
   "Recommended next milestone" line. Read it before deciding anything.
2. The current milestone's **design spec** and **implementation plan** (see §2). Execute the plan
   task-by-task; do not improvise scope.
3. `docs/dev/async-and-eventing-conventions.md` — global best practice (async/await + events-at-the-seams).
4. `docs/dev/milestone-35-...-learnings.md` and `docs/dev/milestone-36-...-learnings.md` — the module
   "carve" pattern (module ports, the outbox→projector seam, drop-and-recreate schema moves,
   strangler ordering). M37 follows the same pattern.

## 1. Non-negotiable rules

- **No AI attribution on commits or PRs.** Do NOT add `Co-Authored-By: <model>`, `🤖 Generated with…`,
  `Generated with …`, or any similar trailer. Commit messages are plain and conventional
  (`feat(scope): …`, `refactor(quotes): …`, `docs: …`, `test: …`). This is strict — scrub it if a tool
  adds it automatically.
- **Trunk-based PR flow into a protected `main`.** Never commit directly to `main`. Work on the
  milestone branch (`feat/milestone-<N>-<slug>`). Deliver via a pull request; squash-merge only after
  CI + review pass.
- **Verify before you commit / before the PR** (all must pass):
  - `dotnet build LIAnsureProtect.slnx --no-restore` → `0 Warning(s), 0 Error(s)`.
  - `dotnet test LIAnsureProtect.slnx --no-build` → all pass (one PostgreSQL opt-in integration test is
    skipped by design unless `LIANSUREPROTECT_RUN_POSTGRES_TESTS=true`).
  - `dotnet ef migrations has-pending-model-changes --context <Ctx> …` → **clean for all three contexts**
    (`SubmissionDbContext`, `NotificationsDbContext`, `UnderwritingDbContext`). Exact commands are in the
    current plan's "Conventions" block.
  - Full local CI before opening the PR: `pwsh ./scripts/run-local-ci.ps1` (spins up Docker PostgreSQL,
    applies all three contexts' migrations, runs backend + frontend). It must print `Local CI passed.`
- **Never weaken or delete a test to make the suite green.** If a test fails, find the real cause. If a
  behaviour genuinely became eventually-consistent (see §3), convert the test to pump the outbox
  dispatcher then assert — do not remove the assertion. If a test fails for a non-timing reason, it is a
  real bug: fix the code, not the test.
- **Commit per task** (the plan is structured in small tasks). Keep each commit compiling
  (`dotnet build` 0/0). The integration suite may be intentionally red *during* a strangler cut-over; the
  plan's test-rework task restores it — that is expected and called out in the plan.

## 2. Where the current work lives (Milestone 37)

- Branch: `feat/milestone-37-underwriting-evidence` (already created).
- Design spec: `docs/dev/milestone-37-underwriting-evidence-design.md`.
- Implementation plan (execute this): `docs/superpowers/plans/2026-07-01-milestone-37-underwriting-evidence.md`.
- The plan is phased (A: module-outbox + multi-source dispatch foundation; B: evidence request/review
  carve; C: delete legacy + docs + PR). Do the phases/tasks in order.

## 3. Architecture rules to respect (modular monolith)

- The app is a **modular monolith**: `src/Modules/<Context>/{Domain,Application,Infrastructure}` plus a
  legacy layered core (`src/LIAnsureProtect.{Domain,Application,Infrastructure,Api,Worker}`) being
  strangled into modules, on a shared kernel (`src/Platform`, `src/Platform.Abstractions`).
- **A module never references another module or the legacy layers.** It references only its own context's
  projects + the Platform shared kernel. The legacy/host side may reference module **Application** (and,
  transitionally in M37, module **Domain** for event mapping — see the plan). These allowed references
  are enforced by `tests/LIAnsureProtect.UnitTests/Architecture/ProjectReferenceBoundaryTests.cs`, which
  uses **exact-equality** allow-lists. When the plan says to add a new allowed edge, update that test's
  `InlineData` accordingly — do not loosen the test any other way.
- **Cross-context communication is by id + domain events**, not by shared aggregates or cross-schema
  foreign keys. Each module owns its own PostgreSQL schema; cross-context reads go through a port the
  module owns and legacy implements (see M35's `IUnderwritingQuoteContextReader`).
- **Events at the seams, synchronous in the core** (see the async/eventing conventions doc). Cross-context
  side effects flow through the transactional outbox → a projector; consumers are idempotent
  (dedupe on the source outbox-message id) and self-healing. M37 introduces the *module-owned* outbox and
  a source-agnostic, **merge-ordered** dispatcher — follow the plan's Phase A exactly; the merge-sort by
  `CreatedAtUtc` preserves cross-source event ordering.

## 4. Environment & tooling

- OS: Windows. Shells available: PowerShell (`pwsh`) and Git Bash. The local-CI and dev scripts are
  PowerShell (`scripts/*.ps1`).
- .NET SDK per `global.json`; solution file `LIAnsureProtect.slnx`. EF Core tools are repo-local
  (`dotnet tool restore` before any `dotnet ef` command).
- Three EF Core DbContexts, each applied with `--context`. The dev scripts, the guard in
  `scripts/common.ps1`, and `.github/workflows/ci.yml` already handle all three.
- Docker is required only for the full local CI and PostgreSQL-backed tests; day-to-day the endpoint
  integration tests run on in-memory SQLite.

## 5. After implementation (closeout)

- Write a learnings doc `docs/dev/milestone-<N>-<slug>-learnings.md`; update `docs/project-status.md`
  (status + verification lines + the next-milestone recommendation), `CHANGELOG.md`, `README.md`, and
  `docs/architecture/overview.md` if the milestone changed structure.
- Open the PR into `main` with a clear, plain (no-attribution) body summarizing what/why/verification/
  deferred items. Do not self-merge a protected branch.

## 6. Verification quick-reference (copy/paste)

```
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test  LIAnsureProtect.slnx --no-build
dotnet tool restore
dotnet ef migrations has-pending-model-changes --context SubmissionDbContext    --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj                                                     --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext  --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj  --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
pwsh ./scripts/run-local-ci.ps1
```
