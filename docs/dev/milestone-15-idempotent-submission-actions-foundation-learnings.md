# Milestone 15 - Idempotent Submission Actions Foundation Learnings

This document records the implementation notes, architecture decisions, process flow, tests, and verification path for `Milestone 15 - Idempotent Submission Actions Foundation`.

Milestone 12 added the submit action.
Milestone 13 made submit domain events durable through the transactional outbox.
Milestone 14 taught the Worker how to process pending outbox messages locally.

Milestone 15 protects the current write endpoints from duplicate client retries.

## Goal

The goal is this rule:

```text
When a client retries the same important POST request with the same Idempotency-Key,
the API returns the same stored response instead of running the write again.
```

Simple analogy:

```text
The Idempotency-Key is a claim ticket.

First request:
  The clerk accepts the ticket, does the work, and writes down the result.

Retry with the same ticket and same request:
  The clerk reads back the recorded result.

Retry with the same ticket but different request:
  The clerk refuses, because that ticket was already used for something else.
```

The important production behavior is not only "return the same HTTP response."

The important production behavior is:

```text
same safe retry
  -> no duplicate submission
  -> no duplicate submit transition
  -> no duplicate outbox message
```

## Implemented Scope

Milestone 15 applies idempotency to every currently applicable protected POST endpoint:

```text
POST /api/v1/submissions
POST /api/v1/submissions/{submissionId}/submit
```

Why both:

- `POST /api/v1/submissions` creates a new draft. Duplicate client retries could create duplicate draft submissions.
- `POST /api/v1/submissions/{submissionId}/submit` changes state and creates an outbox message. Duplicate client retries could otherwise create duplicate downstream effects or confusing conflict responses.

Future rule:

```text
Every future important protected POST action should be reviewed for idempotency.
If retrying it can create duplicate state or duplicate side effects, it should use this same pattern.
```

Future examples:

- quote request
- document upload finalization
- underwriting task creation
- bind policy
- create payment intent or external provider request
- enqueue notification or downstream work

## Storage Decision

Idempotency records are stored in PostgreSQL in the same application database.

The new table is:

```text
idempotency_records
```

This is deliberately not Redis and not a separate NoSQL database.

Why PostgreSQL:

- PostgreSQL is already the system of record for this project.
- Idempotency records protect durable business writes, so the records should also be durable.
- The idempotency reservation, submission change, outbox message, and stored response can use the same EF Core `SubmissionDbContext`.
- The unique index on the idempotency key gives the database a hard duplicate-key guard.
- A separate database would make the first implementation harder because the app would need cross-store consistency.

Why not Redis for this milestone:

- Redis is planned as a cache, not the official record.
- Cache entries can expire or be evicted.
- Losing an idempotency record can allow a duplicate write later.

Why not NoSQL for this milestone:

- NoSQL can be useful for notification inboxes or read models later.
- The first idempotency guarantee belongs next to the business write it protects.
- PostgreSQL is already the correct durability boundary for submissions and outbox rows.

Simple analogy:

```text
Submission rows and outbox rows are official paperwork.
Idempotency records are the receipt book for that paperwork.

The receipt book belongs in the same filing office,
not on a sticky note or in a separate building.
```

## Table Shape

The migration `AddIdempotencyRecords` creates:

```text
id
key
owner_user_id
action_name
request_fingerprint
status
response_status_code
response_body
response_content_type
response_location
created_at_utc
completed_at_utc
```

The table has a unique index:

```text
ux_idempotency_records_key
```

Important fields:

- `key`: the client-provided `Idempotency-Key` header.
- `owner_user_id`: the authenticated user id from `ICurrentUser`.
- `action_name`: the protected action, such as `Submissions.Create` or `Submissions.Submit`.
- `request_fingerprint`: a SHA-256 hash of the method, route template, and request body/route data.
- `status`: starts as `InProgress`, then changes to `Completed`.
- `response_status_code`: the HTTP status code to replay.
- `response_body`: the JSON response body to replay.
- `response_content_type`: currently `application/json`.
- `response_location`: stores the `Location` header for `201 Created` create-submission responses.

Why store the response:

```text
A retry should not merely say "already done."
It should return the same useful result the first request returned.
```

For example, create submission returns the same `submissionId`, same status, and same `Location` header.

## Components

### Application Contract

Files:

```text
src/LIAnsureProtect.Application/Common/Idempotency/IIdempotencyService.cs
src/LIAnsureProtect.Application/Common/Idempotency/IdempotencyRequest.cs
src/LIAnsureProtect.Application/Common/Idempotency/IdempotencyActionResponse.cs
src/LIAnsureProtect.Application/Common/Idempotency/IdempotencyExecutionResult.cs
src/LIAnsureProtect.Application/Common/Idempotency/IdempotencyExecutionStatus.cs
```

What it does:

- Defines the idempotency service contract.
- Keeps the API controller from knowing EF Core details.
- Gives the Infrastructure layer the responsibility for persistence and transaction behavior.
- Stores a serialized action response so the same response can be replayed.

Why the contract is in Application:

```text
API:
  "I have an important write action that may need retry protection."

Application contract:
  "Here is the idempotency promise the API can use."

Infrastructure:
  "I will fulfill that promise with PostgreSQL."
```

### Infrastructure Persistence

Files:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Idempotency/IdempotencyRecord.cs
src/LIAnsureProtect.Infrastructure/Persistence/Idempotency/IdempotencyRecordStatus.cs
src/LIAnsureProtect.Infrastructure/Persistence/Idempotency/EfCoreIdempotencyService.cs
src/LIAnsureProtect.Infrastructure/Persistence/Configurations/IdempotencyRecordConfiguration.cs
src/LIAnsureProtect.Infrastructure/Persistence/SubmissionDbContext.cs
```

What it does:

- Adds `SubmissionDbContext.IdempotencyRecords`.
- Maps the table through EF Core.
- Reserves an idempotency key before running the protected write.
- Runs the protected write inside the same database transaction.
- Stores the response after the write completes.
- Replays the stored response for safe matching retries.
- Rejects unsafe key reuse.

The key class is `EfCoreIdempotencyService`.

Its simplified job:

```text
Look for the key.

If the key already exists:
  compare owner + action + request fingerprint
  if they match and the record is completed, replay the stored response
  if they do not match, reject the request

If the key does not exist:
  start a database transaction
  insert an InProgress idempotency record
  run the protected write action
  store the final response on the idempotency record
  commit the transaction
```

### API Controller

File:

```text
src/LIAnsureProtect.Api/Controllers/SubmissionsController.cs
```

What it does:

- Reads the `Idempotency-Key` request header.
- Uses the current authenticated user id.
- Creates an action name:
  - `Submissions.Create`
  - `Submissions.Submit`
- Creates a request fingerprint.
- Calls `IIdempotencyService.ExecuteAsync(...)` around the existing write action.
- Converts a stored idempotency response back to an HTTP response.

The existing authorization still runs first:

```text
Authentication
  -> Authorization policy
  -> Controller action
  -> Idempotency service
  -> MediatR command
  -> EF Core/PostgreSQL
```

This matters because idempotency must not bypass ownership or role checks.

## Request Fingerprint

The request fingerprint protects against unsafe key reuse.

The API hashes:

```text
HTTP method
route template
body or route data
```

For create submission:

```text
POST
/api/v1/submissions
CreateSubmissionRequest body
```

For submit submission:

```text
POST
/api/v1/submissions/{submissionId}/submit
submissionId route value
```

Why this matters:

```text
Same key + same request:
  safe retry

Same key + different body:
  conflict

Same key + different submission id:
  conflict

Same key + different action:
  conflict

Same key + different user:
  conflict
```

Simple analogy:

```text
The key is the claim ticket number.
The fingerprint is the description of what the ticket was for.

Ticket #123 for "create Jane's submission" cannot later be used for
"submit a different submission."
```

## Create Submission Flow

Endpoint:

```text
POST /api/v1/submissions
Idempotency-Key: create-submission-key-1
```

First request:

```text
Browser/API client
  -> POST /api/v1/submissions
  -> Submissions.Create policy
  -> SubmissionsController.Create
  -> read Idempotency-Key
  -> build fingerprint from method + route + request body
  -> IIdempotencyService.ExecuteAsync(...)
  -> insert idempotency_records row as InProgress
  -> CreateSubmissionCommand
  -> CreateSubmissionCommandHandler
  -> Submission.CreateDraft(...)
  -> ISubmissionRepository.AddAsync(...)
  -> IUnitOfWork.SaveChangesAsync(...)
  -> submissions row inserted
  -> store 201 Created response in idempotency_records
  -> commit transaction
  -> return 201 Created
```

Visual flow:

```text
+-------------------------+
| Client POST create      |
+-------------------------+
            |
            v
+-------------------------+
| Idempotency-Key header  |
+-------------------------+
            |
            v
+-------------------------+
| idempotency_records     |
| reserve key             |
+-------------------------+
            |
            v
+-------------------------+
| Create draft submission |
+-------------------------+
            |
            v
+-------------------------+
| Store response          |
| submissionId + status   |
+-------------------------+
            |
            v
+-------------------------+
| Return 201 Created      |
+-------------------------+
```

Safe retry:

```text
Client retries same POST with same Idempotency-Key and same body
  -> API finds completed idempotency record
  -> owner/action/fingerprint match
  -> API returns stored 201 Created response
  -> no new Submission is created
```

## Submit Submission Flow

Endpoint:

```text
POST /api/v1/submissions/{submissionId}/submit
Idempotency-Key: submit-submission-key-1
```

First request:

```text
Browser/API client
  -> POST /api/v1/submissions/{submissionId}/submit
  -> Submissions.Submit policy
  -> SubmissionsController.Submit
  -> read Idempotency-Key
  -> build fingerprint from method + route + submissionId
  -> IIdempotencyService.ExecuteAsync(...)
  -> insert idempotency_records row as InProgress
  -> SubmitSubmissionCommand
  -> SubmitSubmissionCommandHandler
  -> owner-scoped tracked submission load
  -> Submission.Submit()
  -> SubmissionSubmittedDomainEvent recorded
  -> IUnitOfWork.SaveChangesAsync(...)
  -> submissions status updated
  -> outbox_messages row inserted
  -> store 200 OK response in idempotency_records
  -> commit transaction
  -> return 200 OK
```

Visual flow:

```text
+-------------------------+
| Client POST submit      |
+-------------------------+
            |
            v
+-------------------------+
| Reserve idempotency key |
+-------------------------+
            |
            v
+-------------------------+
| Load owned draft        |
+-------------------------+
            |
            v
+-------------------------+
| Submission.Submit()     |
+-------------------------+
            |
            v
+-------------------------+
| SaveChangesAsync        |
| status + outbox row     |
+-------------------------+
            |
            v
+-------------------------+
| Store stable response   |
+-------------------------+
            |
            v
+-------------------------+
| Return 200 OK           |
+-------------------------+
```

Safe retry:

```text
Client retries same submit request with same Idempotency-Key
  -> API finds completed idempotency record
  -> owner/action/fingerprint match
  -> API returns stored 200 OK response
  -> SubmitSubmissionCommand is not run again
  -> Submission.Submit() is not called again
  -> no duplicate outbox_messages row is created
```

This is the important connection to Milestone 13:

```text
idempotency protects the command from running twice
  -> command does not raise the domain event twice
  -> SaveChangesAsync does not write duplicate outbox rows
```

## Unsafe Reuse Flow

Unsafe key reuse returns `409 Conflict`.

Examples:

```text
Same key + different create body
Same key + different submit submission id
Same key + different action
Same key + different authenticated user
```

Flow:

```text
Client sends request with existing key
  -> API loads idempotency_records row
  -> compares owner_user_id
  -> compares action_name
  -> compares request_fingerprint
  -> mismatch found
  -> return 409 Conflict
  -> do not run command
  -> do not change submissions
  -> do not write outbox message
```

Simple analogy:

```text
You cannot use a receipt for one package to pick up a different package.
The receipt number may be real, but it does not describe this request.
```

## In-Progress Flow

The table supports an `InProgress` status.

That status exists for concurrency safety:

```text
Request A starts and reserves the key.
Request B arrives with the same key before Request A is complete.
Request B should not run the write again.
```

Current behavior:

```text
If the same key is already in progress,
the API returns 409 Conflict and asks the client to retry after the first request finishes.
```

This is intentionally simple for the first production-style foundation.

A future hardening milestone can add:

- short wait/retry inside the API for in-progress matching records
- expiration/recovery handling for abandoned in-progress records
- cleanup job for old completed records
- metrics for replay/conflict/in-progress counts
- tenant-scoped key strategy if users are grouped into organizations

## Why The Header Is Optional

The current endpoints still work without `Idempotency-Key`.

That means:

```text
No Idempotency-Key
  -> old behavior

Idempotency-Key present
  -> duplicate retry protection
```

Why:

- It preserves existing API behavior and tests.
- It lets the frontend or external client opt into retry safety.
- It avoids breaking older clients while the pattern is introduced.

Production guidance:

```text
For high-risk write endpoints, clients should always send Idempotency-Key.
Future API hardening may make the header required for selected endpoints.
```

## TDD Notes

RED tests were added before implementation.

Expected failing behaviors before implementation:

```text
Duplicate create with same key created two submissions.
Duplicate submit with same key returned 409 Conflict on the second call.
Same key with different body was accepted.
Same key across users was accepted.
Same key across actions was accepted.
Migration script did not create idempotency_records.
```

GREEN behavior after implementation:

```text
Duplicate create with same key returns the same 201 response and creates one submission.
Duplicate submit with same key returns the same 200 response and creates one outbox message.
Same key with different body returns 409 Conflict.
Same key across users returns 409 Conflict.
Same key across actions returns 409 Conflict.
Migration script creates idempotency_records and ux_idempotency_records_key.
```

## Files Added Or Updated

Application:

```text
src/LIAnsureProtect.Application/Common/Idempotency/IIdempotencyService.cs
src/LIAnsureProtect.Application/Common/Idempotency/IdempotencyRequest.cs
src/LIAnsureProtect.Application/Common/Idempotency/IdempotencyActionResponse.cs
src/LIAnsureProtect.Application/Common/Idempotency/IdempotencyExecutionResult.cs
src/LIAnsureProtect.Application/Common/Idempotency/IdempotencyExecutionStatus.cs
```

API:

```text
src/LIAnsureProtect.Api/Controllers/SubmissionsController.cs
```

Infrastructure:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Idempotency/IdempotencyRecord.cs
src/LIAnsureProtect.Infrastructure/Persistence/Idempotency/IdempotencyRecordStatus.cs
src/LIAnsureProtect.Infrastructure/Persistence/Idempotency/EfCoreIdempotencyService.cs
src/LIAnsureProtect.Infrastructure/Persistence/Configurations/IdempotencyRecordConfiguration.cs
src/LIAnsureProtect.Infrastructure/Persistence/SubmissionDbContext.cs
src/LIAnsureProtect.Infrastructure/DependencyInjection.cs
src/LIAnsureProtect.Infrastructure/Persistence/Migrations/20260620181233_AddIdempotencyRecords.cs
```

Tests:

```text
tests/LIAnsureProtect.IntegrationTests/SubmissionEndpointTests.cs
tests/LIAnsureProtect.IntegrationTests/DependencyRegistrationTests.cs
```

Documentation:

```text
README.md
CHANGELOG.md
docs/architecture/overview.md
docs/project-status.md
docs/dev/pattern-roadmap-after-milestone-11.md
docs/dev/milestone-15-idempotent-submission-actions-foundation-learnings.md
```

## What To Remember

- Idempotency protects important POST actions from duplicate client retries.
- The current implementation covers both existing protected write endpoints.
- The idempotency record is stored in PostgreSQL, not Redis or NoSQL.
- The idempotency key is compared with owner, action, and request fingerprint.
- Safe retries return the stored response.
- Unsafe key reuse returns `409 Conflict`.
- Submit retries do not create duplicate outbox messages.
- Future important POST endpoints should be reviewed for this same pattern.

## Verification

Focused integration tests:

```powershell
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore
```

Result after implementation:

```text
Passed: 31
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
IntegrationTests: 31 passed, 1 skipped PostgreSQL opt-in test
```

EF Core migration guard:

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

Result:

```text
Local CI passed.
Artifact zip: TestResults\local-ci-20260621-022226.zip
```

What the CI run verified:

- Docker-backed PostgreSQL/pgvector started successfully.
- Migration `20260611012509_CreateSubmissionPersistence` applied.
- Migration `20260619100855_AddSubmissionOwnership` applied.
- Migration `20260620012622_AddTransactionalOutbox` applied.
- Migration `20260620181233_AddIdempotencyRecords` applied.
- Backend build passed with 0 warnings and 0 errors.
- UnitTests passed: 22.
- IntegrationTests passed: 32, including the PostgreSQL opt-in persistence test inside the CI environment.
- Docker Compose config validation passed.
- Frontend production build passed.
- Frontend ESLint passed.
- Frontend Vitest passed: 5 files, 16 tests.
- The CI artifact zip was created.
- The PostgreSQL container, volume, and network were cleaned up.
