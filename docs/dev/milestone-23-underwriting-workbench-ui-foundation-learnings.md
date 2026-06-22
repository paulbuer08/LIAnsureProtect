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

## Implemented Shape

Milestone 23 adds the first underwriter-facing React workbench:

```text
/underwriting/quote-referrals
  -> RequireAuth
  -> UnderwritingQuoteReferralsPage
  -> useQuoteReferrals
  -> GET /api/v1/underwriting/quote-referrals
```

The workbench is intentionally feature-owned:

```text
src/LIAnsureProtect.Web/src/features/underwriting
  api/underwritingApi.ts
  hooks/useQuoteReferrals.ts
  hooks/useUnderwritingActions.ts
  pages/UnderwritingQuoteReferralsPage.tsx
  pages/UnderwritingQuoteReferralsPage.test.tsx
  types.ts
```

That matches the existing frontend pattern from the submissions feature. The page does not introduce a shared API client yet because the current repo pattern keeps each feature slice responsible for its own typed API calls.

## Realistic Workbench Behavior

The first plan was a straightforward list plus action screen. The implemented version is slightly more realistic for specialty underwriting while still staying inside the existing backend surface.

It includes:

- a queue-style list of referred quotes;
- risk tier badges;
- quote expiry urgency;
- premium, requested limit, and retention;
- referral reasons;
- subjectivities;
- client-side queue filters for all referrals, high/severe risk referrals, and referrals expiring within seven days;
- a selected-quote side panel for decision work;
- separate advisory AI and manual decision areas.

This mirrors the real-life idea that underwriters do not only click "approve" or "decline." They first triage which risks need attention, review the reason for referral, compare quoted terms, inspect subjectivities, and then record a reasoned decision.

## Advisory AI UI

The UI can request an advisory review through:

```text
POST /api/v1/underwriting/quote-referrals/{quoteId}/ai-review
```

It displays:

- executive summary;
- positive and negative risk signals;
- control gaps;
- suggested underwriting questions;
- suggested subjectivity candidates;
- citations;
- limitations;
- provider name;
- prompt version;
- output schema version;
- input snapshot hash;
- advisory disclaimer.

The disclaimer is shown next to the AI output because the UI must teach the same boundary enforced by the backend: AI is decision support only.

The manual approve, decline, and adjust forms are visually separate from the AI review panel. This matters because a real underwriter should be able to read AI support without confusing it for a binding underwriting decision.

## Manual Decision UI

The workbench calls the existing human underwriting endpoints:

```text
POST /api/v1/underwriting/quote-referrals/{quoteId}/approve
POST /api/v1/underwriting/quote-referrals/{quoteId}/decline
POST /api/v1/underwriting/quote-referrals/{quoteId}/adjust
```

Approve and decline collect:

```text
reason
notes
```

Adjust collects:

```text
adjusted premium
adjusted retention
updated subjectivities
reason
notes
```

After a successful manual action, the TanStack Query mutation invalidates the referral queue query. If the backend no longer returns the reviewed quote, it disappears from the queue on refetch.

## Why No Backend Expansion Was Added

During planning, a more advanced real-life workbench was considered. Real specialty underwriting systems often include:

- assignment to a named underwriter;
- SLA or due-date tracking;
- persisted work notes;
- audit timelines;
- account/company context;
- broker details;
- document review;
- appetite and guideline checks;
- notification inboxes.

Those are realistic, but most require new backend state, new read models, or new domain rules. Milestone 23 deliberately stays frontend-focused and consumes the backend endpoints already built in Milestones 18 and 22.

The next backend enrichment milestone can add richer referral context, assignment, work notes, or document review when the project is ready to change the system of record.

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

Implementation is complete locally.

Implementation commit:

```text
cc7735a feat: add underwriting workbench UI foundation
```

Implemented:

- Protected `/underwriting/quote-referrals` route.
- Dashboard navigation to the underwriting workbench.
- Frontend underwriting feature slice with API calls, hooks, and typed contracts.
- Queue-style referral list with risk/expiry triage.
- Advisory AI review request and display.
- Manual approve, decline, and adjust forms.
- Queue invalidation after successful manual action.
- Focused frontend tests for route registration, loading, empty, error, list rendering, triage display, AI output, advisory wording, mutation payloads, and dashboard navigation.

Verification:

```text
Focused frontend tests:
  App route, Dashboard navigation, Underwriting workbench page
  3 files passed, 10 tests passed

Full frontend Vitest:
  7 files passed, 24 tests passed

Frontend TypeScript build:
  tsc -b passed

Frontend ESLint:
  eslint . passed

Frontend production build:
  vite build passed

Direct solution build:
  succeeded with 0 warnings and 0 errors

Direct solution test run:
  UnitTests: 38 passed
  IntegrationTests: 64 passed, 1 skipped PostgreSQL opt-in test

EF Core pending model check:
  no pending model changes

Full local CI:
  passed
  UnitTests: 38 passed
  IntegrationTests: 65 passed, including the PostgreSQL opt-in persistence test
  Frontend Vitest: 7 files passed, 24 tests passed
  Artifact zip: TestResults\local-ci-20260622-120530.zip
```

What the CI run verified:

- Docker-backed PostgreSQL/pgvector started successfully.
- All committed migrations applied through `20260622002934_AddAiUnderwritingReviews`.
- Backend build passed with 0 warnings and 0 errors.
- Backend unit and integration tests passed.
- Docker Compose config validation passed.
- Frontend production build passed.
- Frontend ESLint passed.
- Frontend Vitest passed.
- CI artifact zip was created.
- The PostgreSQL container, volume, and network were cleaned up.

## Handoff Recommendation

Recommended next milestone:

```text
Milestone 24 - Underwriting Referral Operations Foundation
```

Suggested direction:

- Keep Milestone 24 backend-focused.
- Enrich referred quote operations with real underwriter workflow state instead of adding more frontend-only display logic.
- Consider assignment, priority, due date/SLA, persisted work notes, and audit timeline events if the existing domain model can support them cleanly.
- Keep advisory AI separate from authority-bearing decisions.
- Do not add document upload, RAG, autonomous AI decisions, or production model credentials unless explicitly approved.
