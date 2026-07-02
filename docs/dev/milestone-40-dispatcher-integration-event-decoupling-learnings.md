# Milestone 40 - Dispatcher Integration Event Decoupling - Learning Notes

## Goal

Milestone 40 reduced the coupling inside the local outbox dispatcher without changing business
behavior.

Before this milestone, `OutboxDispatcher` already knew how to drain more than one outbox source and
merge-order messages by `CreatedAtUtc`. That source side was decoupled in Milestone 37. The remaining
problem was the consumer side: the dispatcher still called two static mapper classes directly:

- `OutboxNotificationMapper`;
- `OutboxReferralOperationMapper`.

Those mappers knew concrete event types from legacy Quoting/Policy code and module-owned Underwriting
evidence code. That made the dispatcher a central place to edit whenever another integration consumer
or event mapping was added.

M40 changed the shape to:

```text
OutboxDispatcher
  -> IOutboxSource[]               (where rows come from)
  -> CreatedAtUtc merge ordering
  -> IOutboxMessageConsumer[]      (who reacts to each row)
       -> ReferralOperationOutboxMessageConsumer
       -> NotificationOutboxMessageConsumer
  -> mark processed / retry / poison
```

The dispatcher now owns orchestration only. It does not know which event maps to a notification, which
event closes a referral operation, or which concrete domain-event CLR type should deserialize a payload.

## What changed

The Platform outbox abstraction gained a consumer contract:

```text
IOutboxMessageConsumer
OutboxMessageConsumerResult
OutboxMessageConsumerStatus
```

That result tells the dispatcher one of four things:

- `NotHandled` - this consumer does not care about the outbox row.
- `Succeeded` - this consumer handled the row.
- `TransientFailure` - keep the row pending and schedule retry.
- `PermanentFailure` - mark the row as exhausted/poison.

The notification and referral side effects moved into consumers:

```text
NotificationOutboxMessageConsumer
  -> notification mapper registry
  -> INotificationProjector
  -> INotificationPublisher

ReferralOperationOutboxMessageConsumer
  -> referral-operation mapper registry
  -> IReferralOperationProjector
```

The old static mapper switches were replaced by registered mapper classes:

```text
Mapping/Notifications/
  QuoteNotificationMappers.cs
  EvidenceNotificationMappers.cs
  NotificationMessageFactory.cs

Mapping/ReferralOperations/
  ReferralOperationMappers.cs

Mapping/
  IOutboxMessageMapper.cs
  OutboxMessageMapperRegistry.cs
  OutboxMessageJson.cs
```

Each mapper is keyed by the outbox row's existing `Type` string. That means no database migration was
needed and existing outbox rows remain dispatchable.

## Why this is enough for M40

This milestone deliberately did not move quote, rating, acceptance, policy, or binding tables.

The Quoting module boundary is clearer after M39, but the data still lives in `SubmissionDbContext`.
The dispatcher still has to understand some legacy event payloads because those payloads are what the
legacy outbox stores today. Trying to replace the event contracts and move the quote tables in the same
milestone would have mixed two separate risks:

- dispatcher/consumer infrastructure refactoring;
- broad Quoting persistence refactoring.

M40 keeps the refactor at the dispatch seam. A future Quoting carve can move persistence and event
contract ownership with a cleaner dispatcher already in place.

## Retry and ordering behavior stayed the same

The important behavior before M40 was:

```text
1. Drain all registered outbox sources.
2. Merge-order rows by CreatedAtUtc.
3. Run referral projection before notification publishing for the same row.
4. Project notification inbox/team entries before publishing.
5. Mark the row processed only after every matched side effect succeeds.
6. Leave transient publish failures pending for retry.
7. Mark permanent/exhausted failures as poison.
```

M40 preserves that behavior. The registration order keeps the current same-row side-effect order:

```text
ReferralOperationOutboxMessageConsumer
NotificationOutboxMessageConsumer
```

If notification publishing fails after referral projection succeeds, the source row stays pending.
That is still safe because referral projection and notification projection are idempotent on the source
outbox-message id.

## The test that mattered

The existing `OutboxDispatcherTests` remained the main safety net. They still prove:

- unknown events are marked processed;
- quote notifications publish and record provider metadata;
- transient notification failures stay pending;
- permanent notification failures become poison failures;
- customer notifications write personal inbox entries;
- operations notifications write team inbox entries;
- duplicate inbox projection is ignored;
- multiple sources are drained;
- cross-source rows are processed in `CreatedAtUtc` order;
- module evidence events still process before later legacy quote decision events.

M40 added one new extension-point test: a test-only `RecordingOutboxConsumer` handles
`SubmissionSubmittedDomainEvent` without changing `OutboxDispatcher`. That proves the dispatcher is now
open to new consumers through registration instead of direct edits.

## What remains later

Milestone 40 is not the production message bus.

The local dispatcher still runs in-process and still reads PostgreSQL outbox rows directly. The AWS
messaging milestone remains later in the roadmap:

```text
transactional outbox
  -> local dispatcher
  -> SNS/SQS adapter later
  -> queue consumers / DLQ / replay / archive
```

M40 makes that later step easier because the dispatcher no longer hardcodes today's notification and
referral-operation mapping calls.

## Verification

Fresh verification for M40 closeout:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
dotnet tool restore
dotnet ef migrations has-pending-model-changes --context SubmissionDbContext    --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj                                                     --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext  --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj  --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
pwsh ./scripts/run-local-ci.ps1
```

Results:

- build passed with 0 warnings and 0 errors;
- focused `OutboxDispatcherTests` passed with 15 tests;
- full solution tests passed with UnitTests 66 and IntegrationTests 125, with one PostgreSQL opt-in
  test skipped outside the Docker-backed local CI path;
- all three EF Core pending-model checks reported no pending model changes;
- full local CI passed against fresh Docker PostgreSQL, all three context migration sets, backend
  tests, frontend install/build/lint/tests, artifact creation, and Docker cleanup;
- local CI artifact: `TestResults\local-ci-20260702-153844.zip`.

## Next milestone

The recommended next milestone is:

```text
Milestone 41 - Observability
```

That should add production-grade visibility around the modular monolith and dispatcher path before the
later S3 and SNS/SQS milestones introduce more external infrastructure.
