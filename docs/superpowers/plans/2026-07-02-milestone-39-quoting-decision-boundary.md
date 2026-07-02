# Milestone 39 - Quoting Decision Boundary Implementation Plan

> Branch: `feat/milestone-39-quoting-decision-boundary`
>
> Source design: `docs/dev/milestone-39-quoting-decision-boundary-design.md`
>
> Goal: make final quote underwriting decision authority explicit as a Quoting boundary, without forcing
> approve/decline/adjust into the Underwriting module.

## Conventions

- Read first:
  - `AGENTS.md`
  - `docs/project-status.md`
  - `docs/dev/milestone-39-quoting-decision-boundary-design.md`
  - `docs/dev/milestone-38-underwriting-evidence-documents-learnings.md`
  - `docs/dev/milestone-37-underwriting-evidence-learnings.md`
  - `docs/dev/milestone-36-underwriting-referral-operations-learnings.md`
  - `docs/dev/milestone-35-underwriting-ai-review-module-learnings.md`
  - `docs/dev/async-and-eventing-conventions.md`
- Do not move final approve/decline/adjust commands into Underwriting.
- Do not start a broad quote/rating/policy table migration in this milestone unless the design is revised
  first.
- Public API routes and React behavior should stay stable.
- Keep every commit compiling. Before each commit, run `dotnet build LIAnsureProtect.slnx --no-restore`
  and verify 0 warnings / 0 errors.
- Do not weaken tests. If behavior is eventually consistent, pump the outbox dispatcher and assert.
- Do not loosen module-boundary tests. Only update exact allow-lists for real project references.

## Phase A - Confirm The Decision Boundary

### Task A1 - Baseline and boundary inventory

Goal: prove the starting state and identify every final-decision dependency before moving anything.

Steps:

- Run:

```powershell
git status --short --branch
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-build --filter "QuoteUnderwritingReviewTests|ProjectReferenceBoundaryTests"
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-build --filter UnderwritingReferralEndpointTests
```

- Inventory references to:
  - `ApproveReferral`
  - `DeclineReferral`
  - `AdjustReferral`
  - `QuoteUnderwritingReview`
  - `QuoteUnderwritingDecisionRecordedDomainEvent`
  - `IQuoteRepository.AddUnderwritingReviewAsync`
- Write down the smallest viable movement path in this plan before code changes continue.

Commit:

```text
docs: document quoting decision boundary inventory
```

### Task A2 - Add Quoting module skeleton if still justified

Goal: create a clear home for Quoting contracts without moving tables yet.

Expected changes if A1 confirms the shape:

- Add `src/Modules/Quoting/LIAnsureProtect.Modules.Quoting.Domain`.
- Add `src/Modules/Quoting/LIAnsureProtect.Modules.Quoting.Application`.
- Add `src/Modules/Quoting/LIAnsureProtect.Modules.Quoting.Infrastructure`.
- Register the projects in `LIAnsureProtect.slnx`.
- Add a minimal `AddQuotingModule(...)` composition entry point if needed.
- Update `ProjectReferenceBoundaryTests` with exact allowed references for the new module projects.
- Add a migration test only if a `DbContext` is introduced. Prefer no new `DbContext` in this task.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-build --filter ProjectReferenceBoundaryTests
```

Commit:

```text
feat: add quoting module boundary skeleton
```

## Phase B - Move The Command Boundary, Not The Quote Aggregate

### Task B1 - Introduce Quoting Application decision commands or ports

Goal: make final referral decision commands Quoting-owned at the Application boundary while preserving
the existing `Quote` aggregate and `SubmissionDbContext` persistence.

Expected changes:

- Move or wrap:
  - `ApproveQuoteReferralCommand`
  - `DeclineQuoteReferralCommand`
  - `AdjustQuoteReferralCommand`
  - command handlers and result records
- Keep the handlers calling `Quote.ApproveReferral(...)`, `Quote.DeclineReferral(...)`,
  and `Quote.AdjustReferral(...)`.
- Keep audit row creation and `QuoteUnderwritingDecisionRecordedDomainEvent` behavior unchanged.
- Keep the API controller routes unchanged; only namespaces/handler registrations should change.
- If a temporary legacy adapter is needed, make it explicit and delete it before closeout if no caller remains.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-build --filter "QuoteUnderwritingReviewTests|ProjectReferenceBoundaryTests"
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-build --filter UnderwritingReferralEndpointTests
```

Commit:

```text
refactor: move quote referral decision commands to quoting boundary
```

### Task B2 - Preserve referral-operation projection behavior

Goal: prove that Quoting decisions still close Underwriting referral operations through events.

Expected changes:

- Add or update focused integration coverage for:
  - approve closes/refines the operation timeline through the dispatcher;
  - decline closes/refines the operation timeline through the dispatcher;
  - adjust preserves the expected quote terms/audit behavior and projects the decision;
  - tests pump the dispatcher before asserting module operation/timeline state.
- Do not add sleeps.
- Do not weaken existing audit-row or quote-status assertions.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-build --filter UnderwritingReferralEndpointTests
```

Commit:

```text
test: cover quoting decision projection boundary
```

## Phase C - Cleanup and Closeout

### Task C1 - Remove obsolete legacy seams created during M39

Goal: leave no duplicate command path behind.

Expected changes:

- Delete any temporary wrappers or old command files no longer called.
- Verify `rg "ApproveQuoteReferralCommand|DeclineQuoteReferralCommand|AdjustQuoteReferralCommand|AddUnderwritingReviewAsync|QuoteUnderwritingReview" src tests` shows only intentional Quoting/legacy persistence, tests, and historical migrations.
- Keep `Quote` and `QuoteUnderwritingReview` in the legacy Domain if the milestone chose boundary preparation rather than table movement.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-build --filter ProjectReferenceBoundaryTests
```

Commit:

```text
refactor: remove obsolete quote decision seams
```

### Task C2 - Full verification

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
dotnet tool restore
dotnet ef migrations has-pending-model-changes --context SubmissionDbContext    --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj                                                     --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext  --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj  --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
```

If Docker is available:

```powershell
pwsh ./scripts/run-local-ci.ps1
```

### Task C3 - Documentation closeout

Expected changes:

- Write `docs/dev/milestone-39-quoting-decision-boundary-learnings.md`.
- Update `docs/project-status.md`.
- Update `CHANGELOG.md`.
- Update `README.md`.
- Update `docs/architecture/overview.md`.
- Update `docs/dev/production-transformation-roadmap.md` only if the implementation changes the planned M40/M41 sequence.

Verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
```

Commit:

```text
docs: close quoting decision boundary milestone
```

## PR

Before opening the PR:

```powershell
git log main..HEAD --format=%B
```

Confirm there is no `Co-authored-by`, `Generated with`, robot emoji, or similar AI attribution trailer.

Open a PR into `main` with:

- what changed;
- why it changed;
- verification commands/results;
- deferred work.
