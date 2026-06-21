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
src/LIAnsureProtect.Application/Underwriting/Ai/*
src/LIAnsureProtect.Application/Quotes/*
```

Infrastructure:

```text
src/LIAnsureProtect.Infrastructure/Underwriting/Ai/*
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

Not started yet.
