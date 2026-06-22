# Milestone 22 - AI Underwriting Assistant Foundation Learnings

This document records the planning notes, implementation decisions, verification path, and closeout notes for `Milestone 22 - AI Underwriting Assistant Foundation`.

## Starting Point

Milestone 22 starts from the Milestone 21 closeout commit:

```text
18c502a docs: close notification and outbox publishing milestone
```

Milestone 21 added provider-shaped notification publishing on top of the transactional outbox, including retry/failure metadata and `QuoteAcceptedDomainEvent` capture.

That gives Milestone 22 enough quote, referral, underwriting, policy, and notification context to add a realistic AI assistance boundary without giving AI decision authority.

## Goal

Add advisory-only AI support for underwriting review.

In simple English:

- underwriters still make every approve, decline, adjust, bind, and issue decision;
- AI can summarize risk context and suggest questions or considerations;
- AI output is stored as advice and audit evidence, not as a system-of-record decision;
- AI failure must not block the existing manual underwriting workflow.

## Proposed Scope

Milestone 22 should stay focused on a safe first AI foundation:

- Add an Application-owned `IAiReviewService` boundary.
- Add provider-shaped request/result DTOs for advisory underwriting review.
- Add an Infrastructure local simulated AI review provider so tests do not need real model credentials.
- Add PostgreSQL audit persistence for AI review attempts and outputs.
- Add an underwriter-only endpoint or command for generating an advisory review on referred quote context.
- Store enough input context metadata, prompt version, output summary, citations/context references, status, failure reason, and timestamps for auditability.
- Keep AI output explicitly advisory and separate from quote status, policy status, premium, underwriting decision, and binding state.
- Add focused tests proving AI cannot change insurance decisions, AI failure does not block manual underwriting, and stored output is visibly advisory.

## Implemented Shape

Milestone 22 adds the first AI-assisted underwriting path as a separate advisory command:

```text
POST /api/v1/underwriting/quote-referrals/{quoteId}/ai-review
  -> Quotes.Underwrite authorization policy
  -> GenerateAiUnderwritingReviewCommand
  -> GenerateAiUnderwritingReviewCommandHandler
  -> IQuoteRepository.GetForUnderwritingReviewAsync(...)
  -> IAiReviewService
  -> LocalSimulatedAiReviewService
  -> ai_underwriting_reviews audit row
```

The endpoint lives beside the existing human referral endpoints:

```text
POST /api/v1/underwriting/quote-referrals/{quoteId}/approve
POST /api/v1/underwriting/quote-referrals/{quoteId}/decline
POST /api/v1/underwriting/quote-referrals/{quoteId}/adjust
```

That placement is intentional. The AI review is part of the underwriter workflow, but it is not one of the decision actions.

## Advisory Review Packet

The local simulated provider returns a structured cyber underwriting review packet:

```text
executive summary
positive risk signals
negative risk signals
control gaps
suggested underwriting questions
suggested subjectivity candidates
citations/context references
limitations
advisory disclaimer
```

This is more realistic than a generic summary because real specialty cyber underwriting is not just "approve or decline." Underwriters look for risk drivers, security control gaps, unclear applicant answers, follow-up questions, and subjectivities that may need evidence before bind.

The simulated provider uses only existing quote/referral context:

```text
quote id
submission id
owner user id
premium
requested limit
retention
risk tier
quote status
rating strategy name
subjectivities
referral reasons
prior underwriting review summaries when present
```

It does not read uploaded documents, broker emails, external scans, embeddings, or customer chat content because those features are not implemented yet.

## Persistence Added

Milestone 22 adds PostgreSQL audit table:

```text
ai_underwriting_reviews
  id
  quote_id
  requested_by_user_id
  provider_name
  status
  prompt_version
  output_schema_version
  input_snapshot_hash
  executive_summary
  positive_risk_signals jsonb
  negative_risk_signals jsonb
  control_gaps jsonb
  suggested_underwriting_questions jsonb
  suggested_subjectivity_candidates jsonb
  citations jsonb
  limitations jsonb
  advisory_disclaimer
  failure_reason
  feedback
  created_at_utc
  completed_at_utc
```

Why PostgreSQL:

- AI review output is underwriting audit evidence, not temporary cache data.
- It belongs beside quote, underwriting review, provider attempt, policy, and outbox audit records.
- Prompt version, output schema version, and input snapshot hash allow later troubleshooting and governance review.
- JSONB fields preserve structured output without adding many child tables before the review packet shape stabilizes.

## Guardrails

AI review success writes only `ai_underwriting_reviews`.

It must not change:

```text
quotes.status
quotes.premium
quotes.retention
quotes.subjectivities
quotes.reviewed_by_user_id
quotes.reviewed_at_utc
quotes.underwriting_decision_reason
quotes.underwriting_decision_notes
policies.status
policy binding state
```

AI review failure is also stored as an audit row where possible. Manual underwriter approve, decline, and adjust actions still work after an AI review failure.

The human decision methods remain the only decision path:

```text
Quote.ApproveReferral(...)
Quote.DeclineReferral(...)
Quote.AdjustReferral(...)
Quote.Accept(...)
Quote.MarkBound(...)
```

The AI command does not call any of these methods.

## Important Boundaries

AI must not approve, decline, adjust, price, accept, bind, issue, cancel, renew, reinstate, or close any insurance workflow.

The local domain model and human underwriting commands remain authoritative. AI is a helper that can produce summaries, risk observations, suggested questions, and supporting notes for a human underwriter.

## Recommended Out Of Scope

- Real production model credentials.
- Autonomous underwriting decisions.
- Replacing the cyber rating engine.
- Changing premium, retention, subjectivities, quote status, or policy status from AI output.
- Training or fine-tuning custom models.
- RAG over uploaded documents.
- Document ingestion and embeddings.
- Prompt-management UI.
- Customer-facing AI chat.

These can become later milestones after the advisory boundary, audit model, and guardrails are stable.

## Likely File Areas

Application:

```text
src/LIAnsureProtect.Application/Quotes/Ai/*
src/LIAnsureProtect.Application/Quotes/Commands/GenerateAiUnderwritingReview/*
```

Infrastructure:

```text
src/LIAnsureProtect.Infrastructure/Quotes/Ai/*
src/LIAnsureProtect.Infrastructure/Persistence/*
```

API:

```text
src/LIAnsureProtect.Api/Controllers/*
```

Tests:

```text
tests/LIAnsureProtect.UnitTests/*
tests/LIAnsureProtect.IntegrationTests/*
```

Docs:

```text
README.md
CHANGELOG.md
docs/project-status.md
docs/architecture/overview.md
docs/dev/pattern-roadmap-after-milestone-11.md
docs/dev/milestone-22-ai-underwriting-assistant-foundation-learnings.md
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

Implementation is in progress.

Current focused verification:

```text
AI command handler unit test: 1 passed
AI endpoint, local provider, dependency registration, and migration guard integration tests: 7 passed
Build: succeeded with 0 warnings and 0 errors
Direct solution test run:
  UnitTests: 38 passed
  IntegrationTests: 64 passed, 1 skipped PostgreSQL opt-in test
EF Core pending model check: no pending model changes
Local CI: passed
Local CI UnitTests: 38 passed
Local CI IntegrationTests: 65 passed, including the PostgreSQL opt-in persistence test
Frontend Vitest: 5 files passed, 16 tests passed
Artifact zip: TestResults\local-ci-20260622-104016.zip
```

What the CI run verified:

- Docker-backed PostgreSQL/pgvector started successfully.
- All committed migrations applied, including `20260622002934_AddAiUnderwritingReviews`.
- Backend build passed with 0 warnings and 0 errors.
- Backend unit and integration tests passed.
- Docker Compose config validation passed.
- Frontend production build passed.
- Frontend ESLint passed.
- Frontend Vitest passed.
- CI artifact zip was created.
- The PostgreSQL container, volume, and network were cleaned up.
