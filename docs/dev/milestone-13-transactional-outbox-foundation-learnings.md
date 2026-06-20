# Milestone 13 - Transactional Outbox Foundation Learnings

This document records the implementation notes, architecture decisions, persistence design, tests, and verification results for `Milestone 13 - Transactional Outbox Foundation`.

Milestone 12 made `Submission.Submit()` record a `SubmissionSubmittedDomainEvent` on the in-memory aggregate.

Milestone 13 makes that event durable.

## Goal

The goal is this rule:

```text
When a submission is submitted, the submission status update and the outbox message are saved together.
```

Simple analogy:

```text
Milestone 12:
  The submitted folder got a note attached to it in memory.

Milestone 13:
  The note is copied into the official filing cabinet
  before the transaction is considered complete.
```

## Where The Outbox Lives

The outbox lives in the same PostgreSQL database as the business table:

```text
PostgreSQL database: liansureprotect
  -> submissions
  -> outbox_messages
```

It is not:

- another PostgreSQL database
- DynamoDB
- Redis
- SQS
- a file
- in-memory storage

## Why Same PostgreSQL Database

The transactional outbox pattern solves this problem:

```text
What if the submission is saved,
but the event that should be processed later is lost?
```

The answer is:

```text
Save the business row and the outbox row in the same database transaction.
```

If both rows are in the same PostgreSQL database and saved through the same EF Core `DbContext.SaveChangesAsync(...)`, then they succeed or fail together.

That means the system avoids this broken state:

```text
submission.status = Submitted
outbox message = missing
```

## Why Not NoSQL For This First Outbox

NoSQL is not wrong in general.

It is just the wrong first storage choice for this write-side outbox.

Why:

- The outbox row must be committed atomically with the submission update.
- PostgreSQL can commit both rows in one local transaction.
- A separate NoSQL database cannot join the same simple PostgreSQL transaction.
- Cross-database/distributed transactions would add complexity before the app needs it.
- The current outbox rows are small and append-oriented, which PostgreSQL handles well.

NoSQL may still be useful later for a different job, such as:

- notification inbox read models
- high-scale per-user notification views
- denormalized dashboard projections

Simple rule:

```text
Use PostgreSQL for the write-side outbox because it must be atomic with business data.
Use NoSQL later for read models when the data has already been safely committed.
```

## Performance Consideration

Performance matters, but the first requirement is correctness.

The outbox table is expected to be efficient enough at this stage because:

- rows are small
- writes are append-style inserts
- payload is stored as PostgreSQL `jsonb`
- future dispatchers can query pending rows by indexed columns

Milestone 13 adds this index:

```text
ix_outbox_messages_processed_at_utc_created_at_utc
```

Why:

```text
The future dispatcher will look for rows where processed_at_utc is null
and usually process the oldest pending rows first.
```

Milestone 13 does not optimize beyond that because no dispatcher exists yet.

## Implemented Flow

Submit request:

```text
POST /api/v1/submissions/{submissionId}/submit
  -> SubmitSubmissionCommandHandler
  -> Submission.Submit()
  -> SubmissionSubmittedDomainEvent recorded on Submission.DomainEvents
  -> IUnitOfWork.SaveChangesAsync(...)
  -> SubmissionDbContext.SaveChangesAsync(...)
```

Outbox capture:

```text
SubmissionDbContext.SaveChangesAsync(...)
  -> find tracked Submission aggregates with DomainEvents
  -> serialize each domain event
  -> add OutboxMessage rows
  -> save submission update and outbox insert together
  -> clear DomainEvents after successful save
```

Database result:

```text
submissions.status = Submitted
outbox_messages.type = SubmissionSubmittedDomainEvent
outbox_messages.payload = serialized event JSON
outbox_messages.processed_at_utc = null
```

`processed_at_utc = null` means:

```text
This message is pending future dispatch.
```

## Outbox Table Shape

The outbox table is:

```text
outbox_messages
```

Columns:

```text
id
type
payload
occurred_at_utc
created_at_utc
processed_at_utc
error
```

Meaning:

- `id`: unique outbox message id.
- `type`: event type name, for example `SubmissionSubmittedDomainEvent`.
- `payload`: serialized JSON event data.
- `occurred_at_utc`: when the domain event happened.
- `created_at_utc`: when the outbox row was created.
- `processed_at_utc`: null until a future dispatcher processes the message.
- `error`: reserved for future dispatch failure details.

## Files Added Or Updated

Infrastructure:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxMessage.cs
src/LIAnsureProtect.Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs
src/LIAnsureProtect.Infrastructure/Persistence/SubmissionDbContext.cs
src/LIAnsureProtect.Infrastructure/Persistence/Migrations/20260620012622_AddTransactionalOutbox.cs
src/LIAnsureProtect.Infrastructure/Persistence/Migrations/20260620012622_AddTransactionalOutbox.Designer.cs
src/LIAnsureProtect.Infrastructure/Persistence/Migrations/SubmissionDbContextModelSnapshot.cs
```

Tests:

```text
tests/LIAnsureProtect.IntegrationTests/DependencyRegistrationTests.cs
tests/LIAnsureProtect.IntegrationTests/SubmissionEndpointTests.cs
```

Documentation:

```text
README.md
CHANGELOG.md
docs/architecture/overview.md
docs/project-status.md
docs/dev/milestone-13-transactional-outbox-foundation-learnings.md
docs/dev/pattern-roadmap-after-milestone-11.md
```

## TDD Notes

First RED integration test:

```powershell
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore
```

Failed because:

```text
LIAnsureProtect.Infrastructure.Persistence.Outbox did not exist.
```

Second RED integration test:

```powershell
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore
```

Failed because:

```text
Committed migrations did not create outbox_messages yet.
```

After adding the EF Core migration, focused integration tests passed:

```text
Passed: 25
Skipped: 1 PostgreSQL opt-in test
Failed: 0
```

## Verification

Focused integration tests:

```powershell
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore
```

Result:

```text
Passed: 25
Skipped: 1 PostgreSQL opt-in test
Failed: 0
```

Backend solution build:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
```

Result:

```text
Build succeeded.
Warnings: 0
Errors: 0
```

Backend solution tests:

```powershell
dotnet test LIAnsureProtect.slnx --no-restore
```

Result:

```text
UnitTests: 22 passed
IntegrationTests: 25 passed, 1 skipped PostgreSQL opt-in test
```

EF Core model/migration check:

```powershell
dotnet ef migrations has-pending-model-changes --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj --context SubmissionDbContext --no-build
```

Result:

```text
No changes have been made to the model since the last migration.
```

Full local CI:

```powershell
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

The first local CI attempt in the Codex sandbox could not reach Docker because Docker Desktop access was denied. The command was rerun with approved Docker access.

Result:

```text
Local CI passed.
Artifact zip: TestResults\local-ci-20260620-181324.zip
```

What the CI run verified:

- Docker-backed PostgreSQL/pgvector started successfully.
- Migration `20260611012509_CreateSubmissionPersistence` applied.
- Migration `20260619100855_AddSubmissionOwnership` applied.
- Migration `20260620012622_AddTransactionalOutbox` applied.
- Backend build passed with 0 warnings and 0 errors.
- UnitTests passed: 22.
- IntegrationTests passed: 26, including the PostgreSQL opt-in persistence test inside the CI environment.
- Docker Compose config validation passed.
- Frontend production build passed.
- Frontend ESLint passed.
- Frontend Vitest passed: 5 files, 16 tests.
- The CI artifact zip was created.
- The PostgreSQL container, volume, and network were cleaned up.

## What To Remember

- The transactional outbox table belongs in the same PostgreSQL database as the business data.
- This is about atomic writes, not about message publishing yet.
- `outbox_messages` stores pending durable messages.
- `processed_at_utc` stays null until a later dispatcher milestone.
- No SNS/SQS, email, Worker dispatch, retry, circuit breaker, or idempotency was added in Milestone 13.
