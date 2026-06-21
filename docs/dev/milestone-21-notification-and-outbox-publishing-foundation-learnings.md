# Milestone 21 - Notification And Outbox Publishing Foundation Learnings

This document records the planning notes, implementation decisions, verification path, and closeout notes for `Milestone 21 - Notification And Outbox Publishing Foundation`.

## Starting Point

Milestone 21 starts from the Milestone 20 closeout commit:

```text
4d91665 docs: close quote acceptance and policy binding milestone
```

Milestone 20 added quote acceptance, durable policy binding, simulated binding acknowledgement audit rows, idempotent accept/bind actions, and `PolicyBoundDomainEvent` outbox capture.

That gives Milestone 21 enough real business events to make notification publishing useful.

## Goal

Put the existing transactional outbox to practical use for quote and policy workflow notifications.

In simple English:

- business actions still write domain events into the local database first;
- the Worker reads those durable outbox rows;
- a notification publishing boundary converts selected events into provider-shaped notification messages;
- local test publishing proves the workflow without needing AWS credentials or real email/SMS delivery yet.

## Proposed Scope

Milestone 21 stayed focused on a realistic publishing foundation:

- Added an Application-owned notification publisher boundary.
- Added an Infrastructure local provider-shaped notification publisher.
- Updated Worker dispatch behavior so selected outbox rows are published through the boundary before being marked processed.
- Recorded enough publish attempt metadata to debug success, retry, and failure behavior.
- Added notification message contracts for important quote and policy workflow events.
- Kept publishing retry-safe so one business action does not create duplicate downstream notifications.
- Added focused backend tests and a migration guard for the new outbox metadata.
- Added `QuoteAcceptedDomainEvent` because quote acceptance is a real specialty-insurance communication point before binding.

Implemented event coverage:

- `QuoteGeneratedDomainEvent`
  - `Quoted` quotes map to `quote.ready` for the customer/broker audience.
  - `Referred` quotes map to `quote.referred_for_underwriting` for the underwriting operations audience.
- `QuoteUnderwritingDecisionRecordedDomainEvent`
  - underwriting approve/adjust/decline decisions map to `quote.underwriting_decision_recorded` for the customer/broker audience.
- `QuoteAcceptedDomainEvent`
  - accepted quotes map to `quote.accepted` for the binding operations audience.
- `PolicyBoundDomainEvent`
  - bound policies map to `policy.bound` for the customer/broker audience and include the policy number.

## Implemented Shape

Application owns the notification publishing contract:

```text
INotificationPublisher
NotificationMessage
NotificationPublishResult
NotificationMessageTypes
NotificationAudiences
```

Infrastructure owns the current local provider-shaped implementation:

```text
LocalNotificationPublisher
OutboxNotificationMapper
OutboxDispatcher
OutboxMessage
```

The dispatch flow is now:

```text
outbox_messages pending row
  -> OutboxDispatcher
  -> OutboxNotificationMapper
  -> NotificationMessage
  -> INotificationPublisher
  -> LocalNotificationPublisher
  -> provider message id
  -> OutboxMessage.MarkPublishSucceeded(...)
  -> processed_at_utc stamped
```

If publishing fails transiently:

```text
publisher returns transient failure
  -> publish_attempt_count increments
  -> last_publish_attempt_at_utc is stamped
  -> error stores safe failure reason
  -> next_attempt_at_utc is set
  -> processed_at_utc remains null
```

If publishing fails permanently:

```text
publisher returns permanent failure
  -> publish_attempt_count increments
  -> failed_at_utc is stamped
  -> next_attempt_at_utc remains null
  -> processed_at_utc remains null
```

This keeps the outbox honest: a row is processed only after the notification boundary accepts it. A retryable failure stays pending for a later pass, while a poison failure is visible for investigation.

## Persistence Added

Milestone 21 extends `outbox_messages` with:

```text
publish_attempt_count
last_publish_attempt_at_utc
next_attempt_at_utc
provider_message_id
failed_at_utc
```

It also adds the retry-oriented index:

```text
ix_outbox_messages_dispatch_retry
```

The stable downstream message id is based on the outbox message id:

```text
NotificationMessage.MessageId = OutboxMessage.Id formatted as N
```

That is the local foundation for duplicate-safe downstream publishing later. A future SNS/SQS or email provider can use this message id as its idempotency or correlation key where the provider supports it.

## Important Boundaries

Notifications are downstream communication side effects. They must not approve, decline, price, accept, bind, cancel, renew, or otherwise change insurance coverage.

The local application database and domain model remain authoritative. External notification systems should be treated as delivery mechanisms, not systems of record.

## Recommended Out Of Scope

- Production SNS/SQS publishing.
- Real email or SMS delivery.
- Notification inboxes.
- Read/unread notification state.
- User notification preferences.
- Complex notification templates.
- Webhooks to broker/customer systems.
- Advisory AI underwriting assistance.

These can become later milestones after the publishing boundary, retry behavior, and operational audit model are stable.

## Likely File Areas

Application:

```text
src/LIAnsureProtect.Application/Notifications/*
```

Infrastructure:

```text
src/LIAnsureProtect.Infrastructure/Notifications/*
src/LIAnsureProtect.Infrastructure/Persistence/Outbox/*
src/LIAnsureProtect.Infrastructure/Persistence/*
```

Worker:

```text
src/LIAnsureProtect.Worker/*
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
docs/dev/milestone-21-notification-and-outbox-publishing-foundation-learnings.md
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

Milestone 21 is complete.

Implementation commit:

```text
ed0d073 feat: add notification and outbox publishing foundation
```

Verification:

```text
Focused policy binding unit tests: 7 passed
Focused outbox dispatcher integration tests: 4 passed
Focused dependency registration and migration guard tests: 3 passed
Focused quote acceptance/policy binding endpoint tests: 12 passed
Build: succeeded with 0 warnings and 0 errors
Direct solution test run:
  UnitTests: 37 passed
  IntegrationTests: 60 passed, 1 skipped PostgreSQL opt-in test
EF Core pending model check: no pending model changes
Local CI: passed
Local CI UnitTests: 37 passed
Local CI IntegrationTests: 61 passed, including the PostgreSQL opt-in persistence test
Frontend Vitest: 5 files passed, 16 tests passed
Artifact zip: TestResults\local-ci-20260621-214045.zip
```

What the CI run verified:

- Docker-backed PostgreSQL/pgvector started successfully.
- All committed migrations applied, including `20260621133523_AddOutboxNotificationPublishingMetadata`.
- Backend build passed with 0 warnings and 0 errors.
- Backend unit and integration tests passed.
- Docker Compose config validation passed.
- Frontend production build passed.
- Frontend ESLint passed.
- Frontend Vitest passed.
- CI artifact zip was created.
- The PostgreSQL container, volume, and network were cleaned up.

Recommended next milestone:

```text
Milestone 22 - AI Underwriting Assistant Foundation
```

The next milestone should add advisory-only AI underwriting assistance with governance and human oversight. It should not allow AI to approve, decline, bind, issue, price, cancel, renew, or close any insurance decision.
