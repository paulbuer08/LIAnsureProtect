# Milestone 16 - Idempotency Operational Hardening Foundation Learnings

This document records the implementation notes, architecture decisions, process flow, tests, and verification path for `Milestone 16 - Idempotency Operational Hardening Foundation`.

Milestone 15 added the first production-style idempotency foundation:

```text
POST /api/v1/submissions
POST /api/v1/submissions/{submissionId}/submit
  -> Idempotency-Key
  -> PostgreSQL idempotency_records
  -> safe replay for matching retries
  -> 409 Conflict for unsafe key reuse
```

Milestone 16 hardens that foundation operationally before the roadmap returns to premium calculation strategy work.

## Goal

The goal is this rule:

```text
The idempotency system should be safe to operate over time,
not only correct for one fresh request/retry pair.
```

Simple analogy:

```text
Milestone 15 created the receipt book.
Milestone 16 starts the back-office rule for removing old completed receipts.
```

## Implemented Scope

Implemented:

- Cleanup/expiry behavior for old completed idempotency records.
- Infrastructure-owned cleanup service for `idempotency_records`.
- Worker-side cleanup that runs about once per hour.
- Seven-day retention for completed idempotency records.
- Cleanup query index on `status` and `completed_at_utc`.
- Integration test coverage proving cleanup deletes only expired completed records.
- Dependency-registration and migration-script coverage for the cleanup service and index.

Deferred:

- Recovery behavior for abandoned `InProgress` records.
- Broader observability for completed, replayed, conflicted, and in-progress idempotency outcomes.
- A decision on whether `Idempotency-Key` should become mandatory for selected high-risk endpoints.
- A full checklist for future protected POST endpoints that need idempotency.
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

Milestone 16 starter commit:

```text
6ab5ae5 docs: start idempotency operational hardening milestone
```

## Retention Decision

Completed idempotency records are now retained for seven days.

Why seven days:

- It is long enough for realistic browser, network, or API-client retries.
- It keeps local and future production cleanup behavior easy to understand.
- It avoids keeping replay receipts forever.
- It is conservative for the current project because no payment provider or external billing workflow exists yet.

The rule is:

```text
CompletedAtUtc older than now - 7 days
  -> delete

CompletedAtUtc within the last 7 days
  -> keep

InProgress
  -> keep
```

## Why Only Completed Records Are Deleted

Milestone 16 deliberately deletes only `Completed` records.

That is safe because completed records already contain the final stored response:

```text
status = Completed
response_status_code = stored
response_body = stored
completed_at_utc = set
```

After the retention window, a repeated client request with the same old key is treated like a new request because the receipt has expired.

`InProgress` rows are different:

```text
status = InProgress
response_status_code = null
response_body = null
completed_at_utc = null
```

An `InProgress` row may mean:

- another request is still running
- a request crashed after reserving the key
- the app stopped midway through a transaction boundary
- a retry arrived while the original request was still active

Deleting or reusing those rows needs a clear recovery rule. That is deferred so this milestone does not accidentally weaken the duplicate-write protection created in Milestone 15.

## Components

### `IIdempotencyRecordCleanup`

File:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Idempotency/IIdempotencyRecordCleanup.cs
```

What it does:

- Gives the Worker one small operation for idempotency table maintenance.
- Keeps cleanup query details inside Infrastructure.
- Avoids putting EF Core query logic directly in the Worker.

Method:

```text
DeleteExpiredCompletedRecordsAsync(...)
```

### `EfCoreIdempotencyRecordCleanup`

File:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Idempotency/EfCoreIdempotencyRecordCleanup.cs
```

What it does:

- Finds idempotency records where:
  - `status` is `Completed`
  - `completed_at_utc` is not null
  - `completed_at_utc` is older than the cutoff
- Deletes those records.
- Returns the number of deleted rows.

Simplified flow:

```text
Worker provides cutoff time
  -> cleanup service queries idempotency_records
  -> select expired Completed rows only
  -> remove them
  -> SaveChangesAsync
  -> return deleted count
```

### EF Core Index

Migration:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Migrations/20260620185535_AddIdempotencyRecordCleanupIndex.cs
```

Index:

```text
ix_idempotency_records_status_completed_at_utc
```

Why:

Cleanup filters by `status` and `completed_at_utc`.

The index helps the database find old completed rows without scanning the whole receipt table as it grows.

### Worker Cleanup

File:

```text
src/LIAnsureProtect.Worker/Worker.cs
```

Current Worker responsibilities:

```text
Every polling pass:
  -> dispatch pending outbox messages

About once per hour:
  -> delete expired completed idempotency records
```

The Worker uses a dependency-injection scope per loop, matching the existing outbox dispatcher pattern. That matters because `SubmissionDbContext` is scoped.

Cleanup logs only when rows are deleted:

```text
Deleted {DeletedIdempotencyRecordCount} expired completed idempotency record(s).
```

This is intentionally lightweight observability. It gives a useful operational signal without introducing a metrics backend yet.

## TDD Notes

The RED test was added first:

```text
DeleteExpiredCompletedRecordsAsync_Deletes_Only_Completed_Records_Before_Cutoff
```

Expected failing behavior before implementation:

```text
IIdempotencyRecordCleanup did not exist.
EfCoreIdempotencyRecordCleanup did not exist.
The migration script did not contain the cleanup index.
```

GREEN behavior after implementation:

```text
Expired Completed record is deleted.
Recent Completed record is kept.
Old InProgress record is kept.
Dependency registration provides IIdempotencyRecordCleanup.
Migration script includes ix_idempotency_records_status_completed_at_utc.
```

## Files Added Or Updated

Infrastructure:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Idempotency/IIdempotencyRecordCleanup.cs
src/LIAnsureProtect.Infrastructure/Persistence/Idempotency/EfCoreIdempotencyRecordCleanup.cs
src/LIAnsureProtect.Infrastructure/Persistence/Configurations/IdempotencyRecordConfiguration.cs
src/LIAnsureProtect.Infrastructure/Persistence/Migrations/20260620185535_AddIdempotencyRecordCleanupIndex.cs
src/LIAnsureProtect.Infrastructure/Persistence/Migrations/20260620185535_AddIdempotencyRecordCleanupIndex.Designer.cs
src/LIAnsureProtect.Infrastructure/Persistence/Migrations/SubmissionDbContextModelSnapshot.cs
src/LIAnsureProtect.Infrastructure/DependencyInjection.cs
```

Worker:

```text
src/LIAnsureProtect.Worker/Worker.cs
```

Tests:

```text
tests/LIAnsureProtect.IntegrationTests/IdempotencyRecordCleanupTests.cs
tests/LIAnsureProtect.IntegrationTests/DependencyRegistrationTests.cs
```

Documentation:

```text
README.md
CHANGELOG.md
docs/architecture/overview.md
docs/project-status.md
docs/dev/pattern-roadmap-after-milestone-11.md
docs/dev/milestone-16-idempotency-operational-hardening-foundation-learnings.md
```

## What To Remember

- Idempotency records protect durable writes, so they live in PostgreSQL.
- Completed idempotency records are now retained for seven days.
- Expired completed records are deleted by the Worker.
- Cleanup keeps `InProgress` records because abandoned in-progress recovery needs its own explicit rule.
- The cleanup query has an index on status and completion time.
- This milestone does not add premium calculation, quote generation, SNS/SQS, email, notification inboxes, underwriting queues, distributed cache, payment-provider semantics, or ownership bypasses.

## Verification

Focused integration tests:

```powershell
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~IdempotencyRecordCleanupTests|FullyQualifiedName~DependencyRegistrationTests"
```

Result after implementation:

```text
Passed: 4
Failed: 0
```

Full verification:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

Result after implementation:

```text
Build succeeded.
UnitTests: 22 passed
IntegrationTests: 32 passed, 1 skipped in direct solution test run
EF Core pending model check: no pending model changes
Local CI: passed
Local CI UnitTests: 22 passed
Local CI IntegrationTests: 33 passed, including the PostgreSQL opt-in persistence test
Frontend Vitest: 5 files passed, 16 tests passed
Artifact zip: TestResults\local-ci-20260621-030128.zip
```

What the CI run verified:

- Docker-backed PostgreSQL/pgvector started successfully.
- All committed migrations applied, including `20260620185535_AddIdempotencyRecordCleanupIndex`.
- Backend build passed with 0 warnings and 0 errors.
- Backend unit and integration tests passed.
- Docker Compose config validation passed.
- Frontend production build passed.
- Frontend ESLint passed.
- Frontend Vitest passed.
- CI artifact zip was created.
- The PostgreSQL container, volume, and network were cleaned up.
