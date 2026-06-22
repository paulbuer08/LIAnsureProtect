# Milestone 25 - Underwriting Evidence Request Foundation Learnings

This document records the planning and implementation notes for `Milestone 25 - Underwriting Evidence Request Foundation`.

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

## Implemented Scope

Milestone 25 adds the first realistic evidence-request workflow around referred quotes.

Implemented:

- Underwriter-created evidence requests tied to referred quotes and referral operations.
- Request fields for category, title, description, due date, status, requested-by user id, and timestamps.
- Owner customer/broker read access to evidence requests for their own quote/submission context.
- Owner customer/broker response with text evidence, respondent name/title, and placeholder attachment metadata.
- Underwriter accept and cancel actions for evidence requests.
- Underwriter workbench visibility into open/responded evidence requests.
- Timeline entries when evidence requests are created, responded to, accepted, or cancelled.
- A protected customer/broker evidence response page in the React app.

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

## Storage Decision

Evidence requests are stored in PostgreSQL in `quote_evidence_requests`.

The table stores workflow and audit facts:

- quote id,
- submission id,
- referral operation id,
- owner user id,
- evidence category,
- title and description,
- due date,
- status,
- requester/respondent/reviewer audit fields,
- response text,
- safe attachment metadata.

It deliberately does not store file bytes. Attachment fields are placeholders only:

```text
attachment_file_name
attachment_content_type
attachment_size_bytes
```

That makes the workflow realistic enough for underwriting while keeping S3, private object permissions, download audit, virus scanning, OCR, retention, and legal-hold decisions out of this milestone.

## Status Model

The first status set is intentionally small:

```text
Open
Responded
Accepted
Cancelled
```

`Accepted` means the underwriter accepted the evidence response as adequate for review. It does not mean the quote is approved. The final approve, decline, or adjust actions still own underwriting authority.

If more information is needed after a response, the underwriter creates another evidence request instead of reopening the old one.

## API Shape

Underwriter endpoints:

```text
POST /api/v1/underwriting/quote-referrals/{quoteId}/evidence-requests
POST /api/v1/underwriting/quote-referrals/{quoteId}/evidence-requests/{evidenceRequestId}/accept
POST /api/v1/underwriting/quote-referrals/{quoteId}/evidence-requests/{evidenceRequestId}/cancel
```

Customer/broker owner endpoints:

```text
GET  /api/v1/evidence-requests
POST /api/v1/evidence-requests/{evidenceRequestId}/respond
```

Owner actions are scoped by the authenticated user id. A customer or broker cannot respond to another owner's evidence request.

Creating an evidence request moves referral operations to `WaitingForInformation`. Responding adds timeline evidence but does not automatically move the referral to `ReadyForDecision`, because a human underwriter still has to decide whether the evidence is good enough.

## Frontend Shape

The underwriting workbench now shows:

- open evidence request count,
- responded evidence request count,
- waiting-for-information indicator,
- latest evidence activity,
- create evidence request form,
- accept/cancel evidence controls.

The customer/broker side has a protected `/evidence-requests` page. It lists owner-scoped evidence requests and lets the owner submit response text plus optional attachment metadata.

## Verification Notes

Focused checks during implementation:

```powershell
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-restore --filter QuoteEvidenceRequestTests
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~UnderwritingReferralEndpointTests"
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
```

Frontend focused checks:

```powershell
.\node_modules\.bin\vitest.cmd run src\features\underwriting\pages\UnderwritingQuoteReferralsPage.test.tsx src\features\evidence\pages\EvidenceRequestsPage.test.tsx
.\node_modules\.bin\vitest.cmd run
.\node_modules\.bin\tsc.cmd -b
.\node_modules\.bin\vite.cmd build
.\node_modules\.bin\eslint.cmd .
```

Full local CI passed after restoring frontend dependencies through the project `package-lock.json` path:

```text
TestResults\local-ci-20260622-225547.zip
```

During verification, an accidental direct `pnpm exec vitest` run converted `node_modules` into a pnpm-managed layout and created temporary `pnpm-lock.yaml` / `pnpm-workspace.yaml` files. Those generated files were removed, and the reliable recovery was to run the standard local CI with frontend install enabled so `npm ci` rebuilt `node_modules` from `package-lock.json`. After that, frontend build, lint, and all 27 frontend tests passed inside local CI.

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

## Closeout

Implementation commit:

```text
77cad37 feat: add underwriting evidence request foundation
```

Final verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1
```

Final local CI artifact:

```text
TestResults\local-ci-20260622-225547.zip
```

Review note:

- `git diff --check` passed; the only output was the normal Windows CRLF warning.
- Full local CI passed after the project restored frontend dependencies through the repository `package-lock.json` path.
- No `pnpm-lock.yaml` or `pnpm-workspace.yaml` files were left behind, and the evidence milestone did not change the web package manifest.

## Recommended Next Milestone

The strongest next step is `Milestone 26 - Evidence Request Notification and Follow-up Foundation`.

Why this is the natural next slice:

- Real evidence request workflows depend on timely customer/broker outreach and underwriter awareness when a response arrives.
- The app already has a local notification/outbox foundation, so evidence activity can reuse that boundary without adding production email delivery or notification inboxes yet.
- It keeps full document storage, malware scanning, OCR, RAG, and messaging threads separate while still making the evidence workflow feel more operationally real.
