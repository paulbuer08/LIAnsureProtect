# Milestone 14 - Outbox Dispatcher Foundation Learnings

This document records the implementation notes, architecture decisions, process flow, tests, and verification path for `Milestone 14 - Outbox Dispatcher Foundation`.

Milestone 13 made submitted-domain events durable by writing PostgreSQL `outbox_messages` rows in the same transaction as the submission status update.

Milestone 14 teaches the Worker how to pick up those durable rows and mark them processed.

## Goal

The goal is this rule:

```text
When an outbox message is pending, the Worker can read it and stamp it as processed.
```

Simple analogy:

```text
Milestone 13:
  A staff member writes a task card and places it in the outgoing tray.

Milestone 14:
  A clerk checks the tray, handles the card locally,
  and stamps it with the time it was handled.
```

The important learning step is not external messaging yet.

The important learning step is the handoff between:

```text
durable database row
  -> background Worker
  -> local dispatcher service
  -> processed database stamp
```

## Approved Scope

Included:

- Add the first Worker-side dispatcher path.
- Read pending `outbox_messages` rows from PostgreSQL through EF Core.
- Mark processed rows by setting `processed_at_utc`.
- Keep the dispatcher local and in-process.
- Add focused tests for dispatcher behavior and dependency registration.

Deferred:

- SNS/SQS.
- Email.
- Notification inbox/read model.
- Full retry policy.
- Circuit breaker.
- Idempotency keys.
- Quote generation.
- Underwriting queues.

Why this boundary matters:

```text
Dispatcher foundation:
  Can the Worker find work and mark it handled?

Messaging provider milestone:
  Can the app publish safely to SNS/SQS?

Notification milestone:
  Can the app turn events into user-facing messages?

Retry/idempotency milestone:
  Can the app safely handle duplicate attempts and failures?
```

Each of those deserves its own tests and failure-mode discussion.

## Implemented Flow

Current flow:

```text
Submission.Submit()
  -> SubmissionSubmittedDomainEvent
  -> SubmissionDbContext.SaveChangesAsync(...)
  -> outbox_messages row inserted with processed_at_utc = null
  -> Worker polling loop runs
  -> IOutboxDispatcher.DispatchPendingMessagesAsync(...)
  -> OutboxDispatcher reads pending rows
  -> OutboxMessage.MarkProcessed(...)
  -> SubmissionDbContext.SaveChangesAsync(...)
  -> outbox_messages.processed_at_utc is set
```

Visual process flow:

```text
+-------------------------+
| PostgreSQL outbox table |
+-------------------------+
          |
          | pending rows
          | processed_at_utc is null
          v
+-------------------------+
| LIAnsureProtect.Worker  |
+-------------------------+
          |
          | creates DI scope
          v
+-------------------------+
| IOutboxDispatcher       |
+-------------------------+
          |
          | loads oldest pending rows
          v
+-------------------------+
| OutboxMessage           |
| MarkProcessed(...)      |
+-------------------------+
          |
          | SaveChangesAsync
          v
+-------------------------+
| processed_at_utc set    |
+-------------------------+
```

## Components

### `OutboxMessage`

File:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxMessage.cs
```

What it does:

- Represents one durable message waiting in the PostgreSQL outbox.
- Stores the event `Type`, serialized JSON `Payload`, event occurrence time, outbox creation time, processing time, and error text.
- Now exposes `MarkProcessed(...)`.

Why `MarkProcessed(...)` lives on the entity:

```text
The entity owns its own state change.
The dispatcher asks the message to mark itself processed
instead of setting private fields directly.
```

Analogy:

```text
The dispatcher does not forge the stamp inside the envelope.
It asks the envelope record to stamp itself as handled.
```

### `IOutboxDispatcher`

File:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Outbox/IOutboxDispatcher.cs
```

What it does:

- Defines the local dispatcher contract.
- Gives the Worker one simple operation:

```text
DispatchPendingMessagesAsync(...)
```

Why it exists:

- The Worker should not know query details like `processed_at_utc == null`.
- The Worker should not directly manipulate EF Core entities.
- The dispatcher keeps outbox-processing behavior in one testable place.

### `OutboxDispatcher`

File:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcher.cs
```

What it does:

- Reads pending outbox rows.
- Orders them by `created_at_utc`.
- Processes a small local batch.
- Marks each message processed.
- Saves the changes.

Current query shape:

```text
OutboxMessages
  -> where processed_at_utc is null
  -> order by created_at_utc
  -> take 20
```

Why oldest first:

```text
If ten task cards are in the tray,
start with the card that has waited the longest.
```

Why a small batch:

- It prevents one polling pass from trying to process an unlimited number of rows.
- It keeps the first implementation simple.
- A later milestone can make the batch size configurable if the app needs that.

### `DependencyInjection`

File:

```text
src/LIAnsureProtect.Infrastructure/DependencyInjection.cs
```

What it does:

- Registers `IOutboxDispatcher` to `OutboxDispatcher`.
- Keeps the implementation in Infrastructure because this first dispatcher is directly tied to EF Core and PostgreSQL outbox storage.

Registration shape:

```text
IOutboxDispatcher
  -> OutboxDispatcher
  -> SubmissionDbContext
  -> PostgreSQL outbox_messages
```

### `Worker`

File:

```text
src/LIAnsureProtect.Worker/Worker.cs
```

What it does:

- Runs as a background service.
- Creates a dependency-injection scope each polling pass.
- Resolves `IOutboxDispatcher`.
- Calls `DispatchPendingMessagesAsync(...)`.
- Logs when messages were processed.
- Waits briefly and repeats until the Worker stops.

Why it creates a scope:

```text
Worker is long-running.
SubmissionDbContext is scoped.

So each polling pass gets a fresh scope,
uses a fresh DbContext,
then disposes it.
```

Analogy:

```text
Do not keep one clipboard open all day.
Pick up a fresh clipboard for each tray check,
finish the check,
then put the clipboard away.
```

## Why This Is Not SNS/SQS Yet

The final production-style direction still includes AWS messaging:

```text
Domain event
  -> transactional outbox
  -> local dispatcher
  -> SNS
  -> SQS
  -> downstream worker
```

Milestone 14 stops at:

```text
Domain event
  -> transactional outbox
  -> local dispatcher
  -> mark processed
```

That is intentional.

Before introducing SNS/SQS, the project should first prove:

- the Worker can compose the same Infrastructure services as the API
- the Worker can find pending durable messages
- the Worker can update message state safely
- tests protect the local processing path

## TDD Notes

First RED run:

```powershell
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore
```

Failed because the dispatcher did not exist yet:

```text
IOutboxDispatcher could not be found.
OutboxDispatcher could not be found.
```

That was the expected RED state.

After adding the first implementation, the focused test run first found a composition issue:

```text
Unable to resolve service for type ILogger<OutboxDispatcher>
```

The fix was to remove the dispatcher logging dependency because logging inside the dispatcher was not required for this milestone. The Worker still logs when a polling pass processes messages.

Focused GREEN run:

```powershell
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore
```

Result:

```text
Passed: 26
Skipped: 1 PostgreSQL opt-in test
Failed: 0
```

## Files Added Or Updated

Infrastructure:

```text
src/LIAnsureProtect.Infrastructure/Persistence/Outbox/IOutboxDispatcher.cs
src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcher.cs
src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxMessage.cs
src/LIAnsureProtect.Infrastructure/DependencyInjection.cs
```

Worker:

```text
src/LIAnsureProtect.Worker/Worker.cs
```

Tests:

```text
tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs
tests/LIAnsureProtect.IntegrationTests/DependencyRegistrationTests.cs
```

Documentation:

```text
README.md
CHANGELOG.md
docs/architecture/overview.md
docs/project-status.md
docs/dev/milestone-14-outbox-dispatcher-foundation-learnings.md
docs/dev/pattern-roadmap-after-milestone-11.md
```

## What To Remember

- Milestone 13 writes pending durable messages.
- Milestone 14 reads pending durable messages and marks them processed.
- `processed_at_utc = null` means pending.
- `processed_at_utc` with a timestamp means locally processed.
- The current dispatcher is deliberately local and in-process.
- External messaging and notification behavior are still future milestones.

## Verification

Focused integration tests:

```powershell
dotnet test tests\LIAnsureProtect.IntegrationTests\LIAnsureProtect.IntegrationTests.csproj --no-restore
```

Result:

```text
Passed: 26
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
IntegrationTests: 26 passed, 1 skipped PostgreSQL opt-in test
```

Full local CI:

```powershell
.\scripts\run-local-ci.ps1 -RunFrontendInstall:$false
```

The first local CI attempt in the Codex sandbox could not reach Docker because Docker Desktop access was denied. The command was rerun with approved Docker access.

Result:

```text
Local CI passed.
Artifact zip: TestResults\local-ci-20260621-004342.zip
```

What the CI run verified:

- Docker-backed PostgreSQL/pgvector started successfully.
- Migration `20260611012509_CreateSubmissionPersistence` applied.
- Migration `20260619100855_AddSubmissionOwnership` applied.
- Migration `20260620012622_AddTransactionalOutbox` applied.
- Backend build passed with 0 warnings and 0 errors.
- UnitTests passed: 22.
- IntegrationTests passed: 27, including the PostgreSQL opt-in persistence test inside the CI environment.
- Docker Compose config validation passed.
- Frontend production build passed.
- Frontend ESLint passed.
- Frontend Vitest passed: 5 files, 16 tests.
- The CI artifact zip was created.
- The PostgreSQL container, volume, and network were cleaned up.

## Closeout

Milestone 14 implementation was committed locally as:

```text
eef3f34 feat: add outbox dispatcher foundation
```

The next milestone should stay separate from the local dispatcher foundation. A good next slice is `Milestone 15 - Idempotent Submission Actions Foundation`, where selected important POST actions can become safe to retry without creating duplicate downstream effects.
