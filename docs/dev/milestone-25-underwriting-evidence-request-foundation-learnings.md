# Milestone 25 - Underwriting Evidence Request Foundation Learnings

This document starts the planning and learning notes for `Milestone 25 - Underwriting Evidence Request Foundation`.

## Starting Point

Milestone 25 starts from the Milestone 24 closeout commit:

```text
0e169a9 docs: close underwriting referral operations milestone
```

Current branch:

```text
codex/milestone-25-underwriting-evidence-request-foundation
```

Milestone 24 added durable referral operations state for referred quotes: assignment, priority, SLA due dates, workflow status, append-only notes, internal follow-up tasks, and an operational timeline in the underwriting workbench.

## Recommended Scope

Add the first realistic evidence-request workflow around referred quotes.

Good candidates for this milestone:

- Underwriter-created evidence requests tied to referred quotes and referral operations.
- Request fields such as title, description, due date, status, requested-by user id, and timestamps.
- Owner customer/broker read access to evidence requests for their own quote/submission context.
- Owner customer/broker response with text evidence or placeholder attachment metadata.
- Underwriter workbench visibility into open/responded evidence requests.
- Timeline entries when evidence requests are created and responded to.
- Minimal API/read-model changes needed to support the workflow safely.

## Why This Is Realistic Specialty Insurance

Real underwriting referrals often cannot be decided from the original application alone. Underwriters may need evidence such as MFA screenshots, EDR rollout confirmation, backup test details, incident response plans, loss runs, control attestations, or clarification on prior incidents.

Milestone 24 made the internal desk workflow operationally trackable. Milestone 25 can make `WaitingForInformation` concrete by letting underwriters request supporting evidence and letting the insured side respond.

## Important Boundaries

Keep the first slice narrow.

Milestone 25 should not become:

- full document management,
- production S3 storage,
- virus scanning,
- OCR or document extraction,
- embeddings or RAG,
- autonomous AI document review,
- notification inboxes,
- full broker/customer messaging threads,
- authority-matrix enforcement.

AI remains advisory-only. Evidence responses should support human underwriting, not trigger automated approve, decline, adjust, accept, bind, or issue decisions.

## Likely File Areas

Backend:

- `src/LIAnsureProtect.Domain/Quotes`
- `src/LIAnsureProtect.Application/Quotes`
- `src/LIAnsureProtect.Infrastructure/Persistence`
- `src/LIAnsureProtect.Api/Controllers`
- `tests/LIAnsureProtect.UnitTests`
- `tests/LIAnsureProtect.IntegrationTests`

Frontend:

- `src/LIAnsureProtect.Web/src/features/underwriting`
- possibly `src/LIAnsureProtect.Web/src/features/quotes` or a new customer-facing feature slice if the existing frontend route structure needs it

Docs:

- `README.md`
- `CHANGELOG.md`
- `docs/project-status.md`
- `docs/architecture/overview.md`
- `docs/dev/pattern-roadmap-after-milestone-11.md`
- this learning note

## Planning Questions For The Milestone 25 Session

- Should evidence requests live under the Quotes domain, referral operations, or a separate underwriting evidence area?
- Should the first response be text-only, attachment-metadata-only, or both?
- How should customer/broker ownership be checked from a quote back to the original submission owner?
- Which evidence request statuses are enough for the first slice?
- Should underwriters be able to close/cancel requests in this milestone?
- Should responding to an evidence request automatically move referral operations out of `WaitingForInformation`, or only add timeline evidence?
- Which workbench fields should be updated immediately?
- Is a customer/broker UI needed in this milestone, or is API-first acceptable before adding a frontend route?

## Starter Verification Path

Use the standard local verification path:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```
