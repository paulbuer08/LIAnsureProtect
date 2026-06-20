# Milestone 12 - Submission Submit And Domain Events Foundation Learnings

This document records the implementation notes, architecture decisions, domain-event boundary, tests, and verification results for `Milestone 12 - Submission Submit And Domain Events Foundation`.

Milestone 11 made submissions owner-scoped. Milestone 12 adds the next business transition:

```text
Draft submission -> Submitted submission
```

This is the first transition that is important enough to announce as a domain event.

## Goal

The goal is not to build the whole asynchronous messaging system yet.

The goal is this first business rule:

```text
The owner of a draft submission can submit it.
When the submission is submitted, the Submission aggregate records a SubmissionSubmittedDomainEvent.
```

Simple analogy:

```text
Milestone 11:
  Each submission drawer got an owner label.

Milestone 12:
  The owner can move their draft from "working copy" to "submitted",
  and the folder gets a note saying "this was submitted."

Milestone 13:
  That note will be copied into a durable outbox table
  so a background worker can process it reliably later.
```

## Approved Scope

In scope:

- Add owned submit for draft submissions.
- Keep users limited to their own submissions.
- Add `POST /api/v1/submissions/{submissionId}/submit`.
- Add `SubmitSubmissionCommand`, handler, and result.
- Add a repository method that loads an owned submission for update.
- Use the existing `Submission.Submit()` domain method for the business transition.
- Add the first domain event:
  - `SubmissionSubmittedDomainEvent`
- Keep events in memory on the aggregate for this milestone.
- Add focused unit and integration tests.

Out of scope:

- Transactional outbox table.
- Outbox dispatcher.
- Worker message publishing.
- SNS/SQS.
- Email notification.
- Retry/circuit breaker.
- Idempotency keys.
- Quote generation.
- Underwriting queue.
- Questionnaire.
- Document upload.
- Organization/team ownership.
- Admin bypass.

## The Important Design Answer

The in-memory domain-event collection is temporary.

It is not the permanent production async design.

Milestone 12 intentionally stops here:

```text
Submission.Submit()
  -> record SubmissionSubmittedDomainEvent on Submission.DomainEvents
  -> save submission status change
```

Milestone 13 should continue with:

```text
Submission.DomainEvents
  -> outbox_messages table in the same database transaction
```

A later worker milestone should continue with:

```text
outbox_messages
  -> Worker-hosted dispatcher
  -> notification/email/SNS/SQS adapter later
```

Why this is split across milestones:

- Domain events need a real business action first.
- The outbox needs a real event to persist.
- A dispatcher needs a real outbox table to poll.
- A worker needs durable pending work to process.

Building all of that before the first event would teach the shape, but not the reason.

## Is The Existing Worker Project The Right Worker?

Yes.

The existing project:

```text
src/LIAnsureProtect.Worker
```

is a .NET Worker Service host. It is different from the API host because it is designed for background work instead of HTTP requests.

Current shape:

```text
LIAnsureProtect.Worker
  -> AddApplication()
  -> AddInfrastructure(...)
  -> AddHostedService<Worker>()
  -> RunAsync()
```

That makes it a good future home for:

- outbox dispatching
- queue consumers
- notification processing
- audit processing
- document processing
- AI review processing later

Milestone 12 does not use the Worker project yet because there is no durable outbox table or pending background workload. After Milestone 13 creates `outbox_messages`, the Worker can become useful by reading pending rows and dispatching them.

## Files Added Or Updated

Domain:

```text
src/LIAnsureProtect.Domain/Common/IDomainEvent.cs
src/LIAnsureProtect.Domain/Submissions/Submission.cs
src/LIAnsureProtect.Domain/Submissions/SubmissionSubmittedDomainEvent.cs
```

Application:

```text
src/LIAnsureProtect.Application/Common/Security/ApplicationPolicies.cs
src/LIAnsureProtect.Application/Submissions/ISubmissionRepository.cs
src/LIAnsureProtect.Application/Submissions/Commands/SubmitSubmission/SubmitSubmissionCommand.cs
src/LIAnsureProtect.Application/Submissions/Commands/SubmitSubmission/SubmitSubmissionCommandHandler.cs
src/LIAnsureProtect.Application/Submissions/Commands/SubmitSubmission/SubmitSubmissionResult.cs
```

Infrastructure:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Configurations/SubmissionConfiguration.cs
src/LIAnsureProtect.Infrastructure/Submissions/EfCoreSubmissionRepository.cs
```

API:

```text
src/LIAnsureProtect.Api/Controllers/SubmissionsController.cs
src/LIAnsureProtect.Api/Security/AuthorizationPolicies.cs
```

Tests:

```text
tests/LIAnsureProtect.UnitTests/Submissions/SubmissionTests.cs
tests/LIAnsureProtect.UnitTests/Submissions/SubmitSubmission/SubmitSubmissionCommandHandlerTests.cs
tests/LIAnsureProtect.IntegrationTests/SubmissionEndpointTests.cs
```

Documentation:

```text
README.md
CHANGELOG.md
docs/architecture/overview.md
docs/project-status.md
docs/dev/milestone-12-submission-submit-and-domain-events-foundation-learnings.md
docs/dev/pattern-roadmap-after-milestone-11.md
```

## Submit Flow

HTTP flow:

```text
POST /api/v1/submissions/{submissionId}/submit
  -> Submissions.Submit authorization policy
  -> SubmissionsController.Submit(...)
  -> SubmitSubmissionCommand
  -> SubmitSubmissionCommandHandler
```

Application flow:

```text
SubmitSubmissionCommandHandler
  -> ICurrentUser.UserId
  -> ISubmissionRepository.GetOwnedForUpdateAsync(submissionId, ownerUserId, ...)
  -> Submission.Submit()
  -> IUnitOfWork.SaveChangesAsync(...)
```

Domain flow:

```text
Submission.Submit()
  -> require current status is Draft
  -> set Status to Submitted
  -> record SubmissionSubmittedDomainEvent
```

Repository flow:

```text
EfCoreSubmissionRepository.GetOwnedForUpdateAsync(...)
  -> Where(submission.Id == submissionId)
  -> Where(submission.OwnerUserId == ownerUserId)
  -> SingleOrDefaultAsync(...)
```

The submit repository method does not use:

```csharp
AsNoTracking()
```

Why:

- Submit changes the loaded entity.
- EF Core needs to track the entity so `SaveChangesAsync()` can write the new `Submitted` status.
- List/detail reads still use `AsNoTracking()` because they only display data.

Simple rule:

```text
Showing data only -> no tracking.
Changing loaded data -> tracking.
```

## Authorization And Ownership

Milestone 12 adds a separate policy:

```text
Submissions.Submit
```

For now it allows the same roles as create/read:

```text
Customer
Broker
Admin
```

Role authorization answers:

```text
Is this kind of user allowed to try the submit workflow?
```

Ownership filtering answers:

```text
Is this specific submission owned by this signed-in user?
```

Both checks are needed.

If a user tries to submit another user's submission, the API returns:

```text
404 Not Found
```

Why not `403 Forbidden`:

- `403 Forbidden` can reveal that the submission id exists.
- `404 Not Found` keeps "not yours" looking like "not found."
- This matches the Milestone 11 read-detail behavior.

If a user tries to submit the same submission again, the API returns:

```text
409 Conflict
```

Why:

- The submission exists.
- The user owns it.
- The request conflicts with the current state because only `Draft` submissions can be submitted.

## Domain Event Shape

The first domain event is:

```csharp
public sealed record SubmissionSubmittedDomainEvent(
    Guid SubmissionId,
    string OwnerUserId,
    DateTime OccurredAtUtc) : IDomainEvent;
```

It records:

- which submission was submitted
- which owner submitted it
- when the event happened

It does not send an email.

It does not publish to SNS/SQS.

It does not write to an outbox table yet.

It only says:

```text
A meaningful business fact happened inside the domain.
```

## Why SubmissionSubmitted Is Better Than SubmissionCreated

Creating a submission currently creates a draft.

A draft is private and incomplete. It may still be edited, withdrawn, or abandoned later.

Submitting a draft is a stronger business moment:

```text
The user is done preparing the submission and is asking the system to treat it as submitted.
```

That is a better first event because future workflows can naturally react to it:

- create an underwriting task
- notify a broker or internal queue
- start quote preparation
- audit the submitted event

Those reactions are intentionally deferred.

## EF Core Mapping Note

`Submission.DomainEvents` is an in-memory domain concern.

EF Core should not try to map it as a database relationship or column.

The mapping explicitly ignores it:

```csharp
builder.Ignore(submission => submission.DomainEvents);
```

This keeps the current database schema unchanged in Milestone 12.

Milestone 13 should add a real outbox table instead of trying to save `DomainEvents` directly on the `submissions` row.

## TDD Notes

The RED unit test run failed before implementation because the submit command namespace did not exist yet.

Command:

```powershell
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-restore
```

Expected missing behavior:

```text
LIAnsureProtect.Application.Submissions.Commands.SubmitSubmission did not exist.
SubmitSubmissionCommand did not exist.
SubmitSubmissionCommandHandler did not exist.
SubmitSubmissionResult did not exist.
Submission.DomainEvents did not exist.
SubmissionSubmittedDomainEvent did not exist.
```

The GREEN unit test run passed after implementation:

```text
Passed: 22
Skipped: 0
Failed: 0
```

Integration tests then proved the request-pipeline behavior:

- anonymous submit returns `401 Unauthorized`
- an unauthorized role returns `403 Forbidden`
- owner submit returns `200 OK`
- submitted status is persisted
- cross-owner submit returns `404 Not Found`
- repeated submit returns `409 Conflict`

## Verification

Focused unit tests:

```powershell
dotnet test tests\LIAnsureProtect.UnitTests\LIAnsureProtect.UnitTests.csproj --no-restore
```

Result:

```text
Passed: 22
Skipped: 0
Failed: 0
```

Focused integration tests:

```powershell
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore
```

Result:

```text
Passed: 24
Skipped: 1 PostgreSQL opt-in test
Failed: 0
```

The skipped PostgreSQL test is the existing opt-in PostgreSQL-backed persistence test. It runs during full local CI when the Docker-backed database is started by the script.

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
IntegrationTests: 24 passed, 1 skipped PostgreSQL opt-in test
```

Full local CI:

```powershell
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

The first local CI attempt in the Codex sandbox could not reach Docker because Docker Desktop access was denied. The command was rerun with approved Docker access.

Result:

```text
Local CI passed.
Artifact zip: TestResults\local-ci-20260619-193252.zip
```

What the CI run verified:

- Docker-backed PostgreSQL/pgvector started successfully.
- Migration `20260611012509_CreateSubmissionPersistence` applied.
- Migration `20260619100855_AddSubmissionOwnership` applied.
- Backend build passed with 0 warnings and 0 errors.
- UnitTests passed: 22.
- IntegrationTests passed: 25, including the PostgreSQL opt-in persistence test inside the CI environment.
- Docker Compose config validation passed.
- Frontend production build passed.
- Frontend ESLint passed.
- Frontend Vitest passed: 5 files, 16 tests.
- The CI artifact zip was created.
- The PostgreSQL container, volume, and network were cleaned up.

## What To Remember

- `Submissions.Submit` now protects submit actions.
- Submit is owner-scoped through `OwnerUserId`.
- Cross-owner submit returns `404 Not Found`.
- Repeated submit returns `409 Conflict`.
- `Submission.Submit()` owns the business rule and event recording.
- Domain events are in-memory only for Milestone 12.
- The in-memory event collection is temporary, not the permanent async solution.
- The transactional outbox should come next, before any dispatcher or Worker processing.
- The existing Worker project is the likely host for future outbox dispatching once durable outbox rows exist.
