# Milestone 16 - Idempotency Operational Hardening Foundation Learnings

This document starts the learning notes for `Milestone 16 - Idempotency Operational Hardening Foundation`.

Milestone 15 added the first production-style idempotency foundation:

```text
POST /api/v1/submissions
POST /api/v1/submissions/{submissionId}/submit
  -> Idempotency-Key
  -> PostgreSQL idempotency_records
  -> safe replay for matching retries
  -> 409 Conflict for unsafe key reuse
```

Milestone 16 should harden that foundation operationally before the roadmap returns to premium calculation strategy work.

## Goal

The goal is this rule:

```text
The idempotency system should be safe to operate over time,
not only correct for one fresh request/retry pair.
```

Simple analogy:

```text
Milestone 15 created the receipt book.
Milestone 16 decides how long receipts are kept,
what happens to half-written receipts,
how staff can audit receipt activity,
and which important desks must always issue receipts.
```

## Recommended Scope

Included:

- Cleanup/expiry behavior for old completed idempotency records.
- In-progress recovery behavior for abandoned idempotency records.
- Observability for idempotency outcomes:
  - completed
  - replayed
  - conflicted
  - in progress or recovered
- A clear convention for future protected POST endpoints that need idempotency.
- A decision on whether `Idempotency-Key` should become mandatory for selected high-risk endpoints.

Deferred:

- Premium calculation strategy.
- Quote generation.
- SNS/SQS.
- Email.
- Notification inbox/read model.
- Underwriting queues.
- Distributed cache.
- Full payment-provider idempotency semantics.
- Ownership-bypass behavior.

## Starting Point

Branch:

```text
codex/milestone-16-idempotency-operational-hardening-foundation
```

Starting commit:

```text
72c4eca docs: close idempotent submission actions milestone
```

Milestone 15 implementation commit:

```text
cdc3f86 feat: add idempotent submission actions foundation
```

## Questions To Resolve During Planning

- How long should completed idempotency records be retained in local/dev behavior?
- Should cleanup be a repository method, an idempotency service method, or a Worker-owned operation?
- How should abandoned `InProgress` records be detected?
- Should an abandoned matching `InProgress` record be recoverable, retryable, or explicitly conflicted?
- Which outcome counts should be logged now without introducing a metrics backend yet?
- Should `Idempotency-Key` remain optional for create/submit, or become required for submit first?
- What exact checklist should future POST endpoints follow before they opt into idempotency?

## Planned Verification

Recommended verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```
