# Milestone 26 - Evidence Request Notification and Follow-up Foundation Learnings

This document records the planning and implementation notes for `Milestone 26 - Evidence Request Notification and Follow-up Foundation`.

## Starting Point

Branch:

```text
codex/milestone-26-evidence-request-notification-follow-up-foundation
```

Starting commits:

```text
77cad37 feat: add underwriting evidence request foundation
de42ded docs: close underwriting evidence request milestone
```

Milestone 25 added PostgreSQL-backed evidence requests for referred quotes. Underwriters can create evidence requests by cyber-control category, customer/broker owners can respond with text plus safe attachment metadata, underwriters can accept or cancel requests, and referral operations timelines and queue summaries now reflect evidence activity.

## Implemented Scope

Milestone 26 makes evidence requests operationally realistic without jumping into production email, inboxes, scheduled reminder automation, or full document management.

Implemented:

- Evidence request lifecycle domain events for created, responded, accepted, cancelled, and follow-up reminder activity.
- Transactional outbox capture for those evidence request events through the existing `SubmissionDbContext.SaveChangesAsync(...)` event capture path.
- Local notification mapping for evidence request created, responded, accepted, cancelled, and follow-up reminder messages.
- Owner customer/broker notifications for created, accepted, cancelled, and follow-up events.
- Underwriting-operations notifications when customer/broker owners respond to evidence requests.
- Manual underwriter follow-up action for open evidence requests:

```text
POST /api/v1/underwriting/quote-referrals/{quoteId}/evidence-requests/{evidenceRequestId}/follow-up
```

- Referral timeline entries when a follow-up reminder is sent.
- Computed owner evidence request fields:
  - `isOverdue`
  - `daysUntilDue`
- Computed underwriting queue evidence summary fields:
  - `overdueRequestCount`
  - `nextOpenDueAtUtc`
- React underwriting workbench display for overdue evidence count, next open evidence due date, and the manual follow-up action.
- React owner evidence request page display for due and overdue status.

## Why This Is Realistic Specialty Insurance

Evidence requests are not useful if nobody knows they exist or if overdue items disappear into the queue.

In real underwriting operations:

- customers and brokers are prompted when an underwriter needs additional evidence,
- underwriters need to know when a response arrives,
- overdue evidence needs follow-up before a referral can progress,
- notification attempts and follow-up activity need audit evidence even before a production email or inbox provider exists.

This milestone should use local provider-shaped notification behavior as the proof point, not production delivery.

The extra realism added during planning was the manual follow-up action. A badge-only overdue indicator would show a problem, but it would not let an underwriter do the next real operational step. Manual follow-up is still narrow enough for this milestone because it reuses:

- the existing `Quotes.Underwrite` authorization policy,
- the existing underwriting referral route,
- the existing referral operations timeline,
- the existing transactional outbox,
- the existing local notification publisher.

The milestone deliberately does not add automatic scheduled reminders yet. Scheduled reminders need duplicate suppression, reminder cadence rules, quiet hours, time zones, retry recovery, and operational monitoring. Those concerns belong in a later automation milestone.

## Out Of Scope

Milestone 26 should not become:

- production email/SMS delivery,
- notification inboxes,
- notification preference centers,
- full broker/customer messaging threads,
- real file upload/download or S3 document storage,
- virus scanning,
- OCR/document extraction,
- embeddings or RAG,
- autonomous AI evidence review,
- automated approve/decline/adjust decisions.

AI remains advisory-only. Evidence notification and follow-up should help humans move the workflow, not decide coverage.

## Key Design Decisions

- Lifecycle notifications cover all first evidence request status-changing actions: created, responded, accepted, and cancelled.
- Manual follow-up is represented as its own domain event because it is an operational action even though it does not change evidence request status.
- Follow-up audit evidence lives in `quote_referral_timeline_entries`, not a new reminder table.
- Overdue state is computed at read time from `Status == Open && DueAtUtc < DateTime.UtcNow`.
- No EF migration is needed because the new event collection is ignored by the EF mapping and the follow-up audit uses the existing timeline table.
- Notification recipient identity stays attribute-based for this milestone. Notification messages carry owner user id plus event attributes such as quote id, submission id, evidence request id, requested-by user id, actor user id, category, status, and due date.

## Likely File Areas

Backend:

- `src/LIAnsureProtect.Application/Notifications`
- `src/LIAnsureProtect.Application/Quotes`
- `src/LIAnsureProtect.Domain/Quotes`
- `src/LIAnsureProtect.Infrastructure/Notifications`
- `src/LIAnsureProtect.Infrastructure/Persistence`
- `src/LIAnsureProtect.Worker`
- `tests/LIAnsureProtect.UnitTests`
- `tests/LIAnsureProtect.IntegrationTests`

Frontend:

- `src/LIAnsureProtect.Web/src/features/underwriting`
- `src/LIAnsureProtect.Web/src/features/evidence`

Docs:

- `README.md`
- `CHANGELOG.md`
- `docs/project-status.md`
- `docs/architecture/overview.md`
- `docs/dev/pattern-roadmap-after-milestone-11.md`
- this learning note

## Starter Verification Path

Use the standard local verification path:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

## Verification Notes

Focused backend checks passed:

```powershell
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-restore --filter QuoteEvidenceRequestTests
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~OutboxDispatcherTests"
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~UnderwritingReferralEndpointTests"
```

Backend build and full tests passed:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
```

Frontend checks passed with the bundled Codex Node runtime because `node` is not on the shell PATH in this environment:

```powershell
& "C:\Users\Poy\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe" ".\node_modules\vitest\vitest.mjs" run
& "C:\Users\Poy\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe" ".\node_modules\typescript\bin\tsc" -b
& "C:\Users\Poy\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe" ".\node_modules\vite\bin\vite.js" build
& "C:\Users\Poy\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe" ".\node_modules\eslint\bin\eslint.js" .
```

One intentional testing note: an early attempt to run focused frontend tests through `.\node_modules\.bin\vitest.cmd` failed because `node` is not on PATH. The successful path was to invoke the bundled Node executable directly.

Full local CI passed:

```powershell
& "C:\Program Files\PowerShell\7\pwsh.exe" -Command ".\scripts\run-local-ci.ps1 -RunFrontendInstall:`$false"
```

Final local CI artifact:

```text
TestResults\local-ci-20260622-234846.zip
```

## Closeout

Implementation commit:

```text
1f790e0 feat: add evidence request notification follow-up foundation
```

Final verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
& "C:\Program Files\PowerShell\7\pwsh.exe" -Command ".\scripts\run-local-ci.ps1 -RunFrontendInstall:`$false"
```

Closeout result:

- Milestone 26 is implemented and verified locally.
- The final implementation commit is `1f790e0 feat: add evidence request notification follow-up foundation`.
- The final local CI artifact is `TestResults\local-ci-20260622-234846.zip`.
