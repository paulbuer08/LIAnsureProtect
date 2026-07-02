# Milestone 40 - Dispatcher Integration Event Decoupling - Design

## Goal

Milestone 40 reduces the coupling inside the local outbox dispatcher now that the Quoting decision
boundary is explicit.

The important change is not a new business feature. It is a safer dispatch shape:

```text
OutboxDispatcher
  -> drains every IOutboxSource
  -> merge-orders pending rows by CreatedAtUtc
  -> hands each row to registered consumers
       -> notification consumer
       -> referral-operation consumer
  -> marks the source row processed only after all matched consumers succeed
```

The dispatcher should no longer know concrete domain-event types, notification mapping rules, or
referral-operation mapping rules. Those rules move behind small registered consumers and mapper
registries.

## Why this milestone exists

Milestone 37 made the dispatcher source-agnostic: it can drain the legacy `public.outbox_messages`
table and the module `underwriting.outbox_messages` table in one merge-ordered stream.

Milestone 39 made the final quote referral decision boundary explicit: approve, decline, and adjust
are Quoting commands even though the public workbench routes stay under underwriting.

After those two milestones, the remaining coupling is inside the dispatcher:

- it directly invokes static notification and referral-operation mappers;
- those mappers contain switch statements over event type names;
- adding another integration consumer would require editing the dispatcher;
- adding another mapped event requires editing a centralized mapper.

M40 should replace that shape with registered consumers and mapper registries while preserving the
current behavior.

## In scope

- Add an `IOutboxMessageConsumer` abstraction in `Platform.Abstractions`.
- Add a small result type so consumers can report:
  - not handled;
  - handled successfully;
  - transient failure;
  - permanent failure;
  - optional provider message id for notification publishing.
- Refactor `OutboxDispatcher` so it only drains sources, orders rows, runs consumers, handles retry
  metadata, and saves touched sources.
- Replace the static notification mapper with registered notification mapper classes plus a registry.
- Replace the static referral-operation mapper with registered referral-operation mapper classes plus
  a registry.
- Keep the existing notification projector/publisher flow:
  - project inbox/team entry first;
  - publish through the local provider;
  - mark the outbox row processed only after success.
- Keep the existing referral-operation projector flow:
  - map to the module's context-neutral `ReferralOperationEvent`;
  - project idempotently on source outbox-message id.
- Keep public routes, frontend behavior, database schemas, and EF migrations unchanged.
- Add tests proving the dispatcher depends on registered consumers and keeps existing retry/order
  behavior.

## Out of scope

- Moving `Quote`, rating-provider attempts, quote acceptance, policies, or policy binding tables.
- Replacing the local dispatcher with SNS/SQS. That remains Milestone 43.
- Introducing a new persisted integration-event table.
- Changing public HTTP routes or React contracts.
- Making modules reference other modules or legacy layers.
- Adding a new `DbContext`.

## Design

### Consumer abstraction

`Platform.Abstractions.Outbox` gains a consumer contract:

```csharp
public interface IOutboxMessageConsumer
{
    Task<OutboxMessageConsumerResult> ConsumeAsync(
        IOutboxMessageView outboxMessage,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
```

The dispatcher does not need to know whether a consumer projects to a module, publishes a notification,
or does something else later. It only needs a result:

```text
NotHandled          -> this consumer ignores the event
Succeeded           -> this consumer handled the event
TransientFailure    -> leave row pending and set next retry time
PermanentFailure    -> mark row failed/poison
```

The result can carry a provider message id. Today only the notification consumer uses that id. If no
consumer supplies one, a successfully handled row is marked processed without publish metadata.

### Notification consumer

The notification consumer owns the current notification side effects:

```text
outbox row
  -> notification mapper registry
  -> NotificationMessage
  -> INotificationProjector.ProjectAsync(...)
  -> INotificationPublisher.PublishAsync(...)
  -> consumer result
```

It preserves the existing safety order: inbox/team projection happens before provider publishing and
before the source row is marked processed. If publishing fails, the source row stays pending and the
next dispatch can safely re-run the idempotent projection.

### Referral-operation consumer

The referral-operation consumer owns the current Underwriting operation side effect:

```text
outbox row
  -> referral-operation mapper registry
  -> ReferralOperationEvent
  -> IReferralOperationProjector.ProjectAsync(...)
  -> consumer result
```

The consumer returns `NotHandled` for events the operation does not care about. Projection remains
idempotent on the source outbox-message id.

### Mapper registries

Each mapper is registered as a small class keyed by the outbox row's `Type` value:

```text
QuoteGeneratedDomainEvent
  -> QuoteGeneratedNotificationMapper
  -> QuoteGeneratedReferralOperationMapper

QuoteEvidenceRequestCreatedDomainEvent
  -> EvidenceRequestCreatedNotificationMapper
  -> EvidenceRequestCreatedReferralOperationMapper
```

This keeps event-specific mapping logic close to the consumer that needs it. Adding a new event later
should mean adding a mapper registration, not editing `OutboxDispatcher`.

The event payloads stay unchanged. This milestone deliberately keeps the existing `Type` strings so no
migration is needed and existing rows remain dispatchable.

## Boundary rules

- The new consumer abstraction lives in `Platform.Abstractions` because all outbox sources and future
  consumers can depend on it.
- The concrete consumers and mapper registries live in legacy Infrastructure for this milestone.
  That is acceptable because legacy Infrastructure is still the composition point that can see the
  legacy Quoting events and module Application ports.
- Modules still do not reference legacy layers or other modules.
- Quoting tables remain in `SubmissionDbContext`.
- Underwriting still consumes quote decision events through its projector; it does not own the final
  quote decision.

## Test strategy

The existing `OutboxDispatcherTests` remain the main behavior guard:

- unknown/unmapped events are marked processed;
- mapped notification events are projected and published before processing;
- transient notification failures stay pending;
- permanent notification failures become poison failures;
- both legacy and module outbox sources are drained;
- cross-source messages are processed in `CreatedAtUtc` order;
- module evidence events can still dispatch before legacy quote decision events.

M40 adds coverage that a custom registered consumer can handle a row without changing the dispatcher.
That test proves the new extension point instead of only proving the old built-in mappers.

## Verification

Per task:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
```

Final gates:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
dotnet tool restore
dotnet ef migrations has-pending-model-changes --context SubmissionDbContext    --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj                                                     --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext  --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj  --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
pwsh ./scripts/run-local-ci.ps1
```
