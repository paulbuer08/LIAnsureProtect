# Milestone 23 - Underwriting Workbench UI Foundation Learnings

This document records the planning notes, implementation decisions, verification path, and closeout notes for `Milestone 23 - Underwriting Workbench UI Foundation`.

## Starting Point

Milestone 23 starts from the Milestone 22 closeout commit:

```text
30a576e docs: close AI underwriting assistant milestone
```

Milestone 22 added advisory-only AI underwriting review for referred quotes through:

```text
POST /api/v1/underwriting/quote-referrals/{quoteId}/ai-review
```

Milestone 18 already added the human underwriting endpoints:

```text
GET  /api/v1/underwriting/quote-referrals
POST /api/v1/underwriting/quote-referrals/{quoteId}/approve
POST /api/v1/underwriting/quote-referrals/{quoteId}/decline
POST /api/v1/underwriting/quote-referrals/{quoteId}/adjust
```

That gives Milestone 23 enough backend surface to add a usable underwriter-facing frontend workflow without changing the backend decision rules.

## Goal

Add the first protected underwriting workbench UI.

In simple English:

- underwriters should have one frontend place to see referred quotes;
- the workbench should show the referral reasons, subjectivities, quoted terms, and risk tier;
- the underwriter should be able to request an advisory AI review from the UI;
- the underwriter should still make manual approve, decline, or adjust decisions;
- the UI must not make AI autonomous or change quote/policy state by itself.

## Proposed Scope

Milestone 23 should stay focused on UI consumption of existing backend capabilities:

- Add a protected frontend route for the underwriting workbench, likely `/underwriting/quote-referrals`.
- Add a frontend feature folder such as `src/LIAnsureProtect.Web/src/features/underwriting`.
- Add API client functions and TanStack Query hooks for:
  - listing referred quotes;
  - requesting advisory AI review;
  - approving a referral;
  - declining a referral;
  - adjusting premium/retention/subjectivities.
- Add workbench UI states for loading, empty, error, successful referral list, action success, and action failure.
- Show AI review output as advisory only, with the advisory disclaimer visible near the generated content.
- Keep the existing Auth0 access-token flow and guarded route pattern.
- Add focused frontend tests for route rendering, list states, AI review display, advisory wording, and manual action mutation calls.

## Important Boundaries

Milestone 23 should not change the backend underwriting authority model.

The following should stay out of scope unless explicitly expanded:

- New backend underwriting decision rules.
- New quote status or policy status transitions.
- Real production AI model credentials.
- AI-generated autonomous approve/decline decisions.
- Customer-facing AI chat.
- Document upload, embeddings, or RAG.
- Full underwriter dashboard analytics.
- Notification inboxes.

The UI can call existing backend endpoints, but the backend remains authoritative for validation, authorization, persistence, and state changes.

## Likely File Areas

Frontend:

```text
src/LIAnsureProtect.Web/src/App.tsx
src/LIAnsureProtect.Web/src/pages/DashboardPage.tsx
src/LIAnsureProtect.Web/src/features/underwriting/*
```

Tests:

```text
src/LIAnsureProtect.Web/src/features/underwriting/**/*.test.tsx
src/LIAnsureProtect.Web/src/pages/DashboardPage.test.tsx
```

Docs:

```text
README.md
CHANGELOG.md
docs/project-status.md
docs/dev/pattern-roadmap-after-milestone-11.md
docs/dev/milestone-23-underwriting-workbench-ui-foundation-learnings.md
```

## Starting Verification Path

Use this path before closeout:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

## Closeout

Not started yet.
