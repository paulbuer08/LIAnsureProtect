# Milestone 24 - Underwriting Referral Operations Foundation Learnings

This document starts the planning and learning notes for `Milestone 24 - Underwriting Referral Operations Foundation`.

## Starting Point

Milestone 24 starts from the Milestone 23 closeout commit:

```text
68e094a docs: close underwriting workbench UI milestone
```

Current branch:

```text
codex/milestone-24-underwriting-referral-operations-foundation
```

Milestone 23 added the protected underwriter workbench at `/underwriting/quote-referrals`. That UI can list referred quotes, show risk and expiry triage, request advisory AI review, and submit manual approve, decline, and adjust actions through the existing backend endpoints.

## Recommended Scope

Make the underwriting referral workflow more realistic by adding backend-owned operational state that the workbench can use.

Good candidates for this milestone:

- Referral assignment to an underwriter.
- Referral priority.
- Referral due date or SLA target.
- Persisted underwriter work notes.
- Lightweight audit timeline entries for operational actions.
- Minimal read-model/API changes so the workbench can show and update the new workflow state.

## Why This Is Realistic Specialty Insurance

Real specialty underwriting is not only a final approve or decline decision. It also includes queue ownership, prioritization, follow-up notes, and evidence of what changed over time.

Milestone 23 made the work visible to underwriters. Milestone 24 should make the work operationally trackable.

## Important Boundary

Keep AI advisory-only.

Milestone 24 should not allow AI output to:

- approve a quote,
- decline a quote,
- adjust premium,
- adjust retention,
- accept coverage,
- bind coverage,
- issue a policy,
- assign legal underwriting authority.

AI may still be shown as advisory context if it already exists, but operational workflow changes should be human-owned and auditable.

## Recommended Out Of Scope

- Document upload and review.
- Embeddings, RAG, or document search.
- Real production AI credentials.
- Autonomous AI underwriting decisions.
- Full analytics dashboards.
- Notification inboxes.
- Major frontend redesign beyond the minimum needed to expose new backend fields.

## Likely File Areas

Backend:

- `src/LIAnsureProtect.Domain/Quotes`
- `src/LIAnsureProtect.Application/Quotes`
- `src/LIAnsureProtect.Infrastructure/Persistence`
- `src/LIAnsureProtect.Api/Controllers`
- `tests/LIAnsureProtect.UnitTests`
- `tests/LIAnsureProtect.IntegrationTests`

Frontend, if the backend slice exposes new fields:

- `src/LIAnsureProtect.Web/src/features/underwriting`

Docs:

- `README.md`
- `CHANGELOG.md`
- `docs/project-status.md`
- `docs/architecture/overview.md`
- `docs/dev/pattern-roadmap-after-milestone-11.md`
- this learning note

## Starter Verification Path

Use the usual local verification path:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

## Planning Questions For The Milestone 24 Session

- Should assignment live directly on `Quote`, on an underwriting review record, or on a new referral operations table?
- Should priority be manual-only, calculated from risk/expiry, or both?
- Should due date/SLA be stored explicitly, calculated from referral time, or configurable later?
- Should work notes be append-only audit entries or editable notes?
- Which operational events should appear in the audit timeline?
- Which fields should the existing workbench show immediately?
