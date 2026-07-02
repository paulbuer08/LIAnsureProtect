# Dispatcher Integration Event Decoupling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Decouple `OutboxDispatcher` from concrete event mapping by introducing registered outbox consumers and mapper registries while preserving current notification/referral behavior.

**Architecture:** The dispatcher stays responsible for source draining, merge ordering, retry metadata, and saving touched sources. Notification and referral-operation side effects move behind `IOutboxMessageConsumer` implementations. Concrete event-to-consumer mapping is split into registered mapper classes so new integration events do not require dispatcher edits.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, MediatR, PostgreSQL outbox rows, xUnit integration tests, Microsoft dependency injection.

---

## File Structure

- Create `src/Platform/LIAnsureProtect.Platform.Abstractions/Outbox/IOutboxMessageConsumer.cs`
  - Defines the source-neutral consumer extension point.
- Create `src/Platform/LIAnsureProtect.Platform.Abstractions/Outbox/OutboxMessageConsumerResult.cs`
  - Defines success/not-handled/transient/permanent outcomes.
- Modify `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcher.cs`
  - Replace hard-coded mapper/projector calls with registered consumer execution.
- Create `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/Consumers/NotificationOutboxMessageConsumer.cs`
  - Maps, projects, publishes, and returns provider-aware results for notification events.
- Create `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/Consumers/ReferralOperationOutboxMessageConsumer.cs`
  - Maps and projects referral-operation events.
- Create `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/Mapping/OutboxMessageMapperRegistry.cs`
  - Shared registry for one-output mapper sets.
- Create `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/Mapping/IOutboxMessageMapper.cs`
  - Internal mapper contract keyed by outbox `Type`.
- Create `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/Mapping/OutboxMessageJson.cs`
  - Shared JSON deserialization helper.
- Replace `OutboxNotificationMapper.cs` with notification mapper classes in
  `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/Mapping/Notifications/`.
- Replace `OutboxReferralOperationMapper.cs` with referral-operation mapper classes in
  `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/Mapping/ReferralOperations/`.
- Modify `src/LIAnsureProtect.Infrastructure/DependencyInjection.cs`
  - Register consumers and mapper classes.
- Modify `tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs`
  - Construct dispatcher with consumers and add one custom-consumer extension test.
- Modify closeout docs after implementation:
  - `docs/project-status.md`
  - `docs/architecture/overview.md`
  - `docs/dev/production-transformation-roadmap.md` if the M40 line needs status wording
  - `CHANGELOG.md`
  - `README.md` if doc links need updating
  - `docs/dev/milestone-40-dispatcher-integration-event-decoupling-learnings.md`

## Task 1: Add Planning Docs

**Files:**
- Create: `docs/dev/milestone-40-dispatcher-integration-event-decoupling-design.md`
- Create: `docs/superpowers/plans/2026-07-02-milestone-40-dispatcher-integration-event-decoupling.md`

- [ ] **Step 1: Verify baseline docs exist**

Run:

```powershell
Test-Path docs/dev/milestone-40-dispatcher-integration-event-decoupling-design.md
Test-Path docs/superpowers/plans/2026-07-02-milestone-40-dispatcher-integration-event-decoupling.md
```

Expected before this task: both print `False`.

- [ ] **Step 2: Add the design and plan docs**

Use the design in `docs/dev/milestone-40-dispatcher-integration-event-decoupling-design.md` to lock scope:

```text
Registered consumers and mapper registries replace dispatcher-owned event mapping.
No quote/rating/policy table move.
No route or frontend contract change.
No new DbContext or migration.
```

- [ ] **Step 3: Build before commit**

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
```

Expected: `0 Warning(s), 0 Error(s)`.

- [ ] **Step 4: Commit**

Run:

```powershell
git add -- docs/dev/milestone-40-dispatcher-integration-event-decoupling-design.md docs/superpowers/plans/2026-07-02-milestone-40-dispatcher-integration-event-decoupling.md
git commit -m "docs: start dispatcher integration event decoupling milestone"
```

## Task 2: Add Outbox Consumer Abstractions

**Files:**
- Create: `src/Platform/LIAnsureProtect.Platform.Abstractions/Outbox/IOutboxMessageConsumer.cs`
- Create: `src/Platform/LIAnsureProtect.Platform.Abstractions/Outbox/OutboxMessageConsumerResult.cs`
- Test: `tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs`

- [ ] **Step 1: Add the consumer interface**

Create `IOutboxMessageConsumer.cs`:

```csharp
namespace LIAnsureProtect.Platform.Abstractions.Outbox;

public interface IOutboxMessageConsumer
{
    Task<OutboxMessageConsumerResult> ConsumeAsync(
        IOutboxMessageView outboxMessage,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Add the result type**

Create `OutboxMessageConsumerResult.cs`:

```csharp
namespace LIAnsureProtect.Platform.Abstractions.Outbox;

public enum OutboxMessageConsumerStatus
{
    NotHandled = 0,
    Succeeded = 1,
    TransientFailure = 2,
    PermanentFailure = 3
}

public sealed record OutboxMessageConsumerResult(
    OutboxMessageConsumerStatus Status,
    string? FailureReason = null,
    string? ProviderMessageId = null)
{
    public static OutboxMessageConsumerResult NotHandled()
        => new(OutboxMessageConsumerStatus.NotHandled);

    public static OutboxMessageConsumerResult Succeeded(string? providerMessageId = null)
        => new(OutboxMessageConsumerStatus.Succeeded, ProviderMessageId: providerMessageId);

    public static OutboxMessageConsumerResult TransientFailure(string failureReason)
        => new(OutboxMessageConsumerStatus.TransientFailure, FailureReason: RequireFailureReason(failureReason));

    public static OutboxMessageConsumerResult PermanentFailure(string failureReason)
        => new(OutboxMessageConsumerStatus.PermanentFailure, FailureReason: RequireFailureReason(failureReason));

    private static string RequireFailureReason(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("Failure reason is required.", nameof(failureReason));

        return failureReason.Trim();
    }
}
```

- [ ] **Step 3: Build before commit**

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
```

Expected: `0 Warning(s), 0 Error(s)`.

- [ ] **Step 4: Commit**

Run:

```powershell
git add -- src/Platform/LIAnsureProtect.Platform.Abstractions/Outbox/IOutboxMessageConsumer.cs src/Platform/LIAnsureProtect.Platform.Abstractions/Outbox/OutboxMessageConsumerResult.cs
git commit -m "feat(outbox): add message consumer contract"
```

## Task 3: Refactor Dispatcher To Registered Consumers

**Files:**
- Modify: `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcher.cs`
- Modify: `tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs`

- [ ] **Step 1: Update dispatcher constructor**

Change `OutboxDispatcher` to depend on:

```csharp
IEnumerable<IOutboxSource> sources,
IEnumerable<IOutboxMessageConsumer> consumers
```

Remove direct dispatcher dependencies on:

```csharp
INotificationProjector
INotificationPublisher
IReferralOperationProjector
```

- [ ] **Step 2: Replace hard-coded mapping with consumer loop**

For each ordered message:

```csharp
var providerMessageId = string.Empty;
var failed = false;

foreach (var consumer in consumerList)
{
    var result = await consumer.ConsumeAsync(message, nowUtc, cancellationToken);

    if (result.Status == OutboxMessageConsumerStatus.NotHandled)
        continue;

    if (result.Status == OutboxMessageConsumerStatus.Succeeded)
    {
        if (!string.IsNullOrWhiteSpace(result.ProviderMessageId))
            providerMessageId = result.ProviderMessageId;

        continue;
    }

    var nextAttemptNumber = message.PublishAttemptCount + 1;
    var exhausted = result.Status == OutboxMessageConsumerStatus.PermanentFailure
        || nextAttemptNumber >= MaxPublishAttempts;

    message.MarkPublishFailed(
        nowUtc,
        result.FailureReason ?? "Outbox message consumer failed.",
        exhausted ? null : nowUtc.Add(RetryDelay),
        exhausted);

    failed = true;
    break;
}

if (failed)
    continue;

if (!string.IsNullOrWhiteSpace(providerMessageId))
    message.MarkPublishSucceeded(nowUtc, providerMessageId);
else
    message.MarkProcessed(nowUtc);

processedCount++;
```

- [ ] **Step 3: Add a test-only custom consumer**

In `OutboxDispatcherTests.cs`, add a nested consumer:

```csharp
private sealed class RecordingOutboxConsumer : IOutboxMessageConsumer
{
    public List<Guid> HandledMessages { get; } = [];

    public Task<OutboxMessageConsumerResult> ConsumeAsync(
        IOutboxMessageView outboxMessage,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (outboxMessage.Type != nameof(SubmissionSubmittedDomainEvent))
            return Task.FromResult(OutboxMessageConsumerResult.NotHandled());

        HandledMessages.Add(outboxMessage.Id);
        return Task.FromResult(OutboxMessageConsumerResult.Succeeded());
    }
}
```

Add a test proving a registered consumer can handle an event without dispatcher changes.

- [ ] **Step 4: Temporarily adapt existing test construction**

Until Task 4 adds real consumers, update `CreateDispatcher(...)` to pass test consumers explicitly.

- [ ] **Step 5: Build before commit**

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
```

Expected: `0 Warning(s), 0 Error(s)`.

- [ ] **Step 6: Commit**

Run:

```powershell
git add -- src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcher.cs tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs
git commit -m "refactor(outbox): dispatch through registered consumers"
```

## Task 4: Add Notification And Referral Consumers With Mapper Registries

**Files:**
- Create: `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/Consumers/NotificationOutboxMessageConsumer.cs`
- Create: `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/Consumers/ReferralOperationOutboxMessageConsumer.cs`
- Create: `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/Mapping/IOutboxMessageMapper.cs`
- Create: `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/Mapping/OutboxMessageMapperRegistry.cs`
- Create: `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/Mapping/OutboxMessageJson.cs`
- Replace: `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxNotificationMapper.cs`
- Replace: `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxReferralOperationMapper.cs`
- Modify: `src/LIAnsureProtect.Infrastructure/DependencyInjection.cs`
- Modify: `tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs`

- [ ] **Step 1: Add the mapper contract**

Create `IOutboxMessageMapper<TOutput>` with `EventType` and `Map(...)`.

- [ ] **Step 2: Add the mapper registry**

Create `OutboxMessageMapperRegistry<TOutput>` that builds a dictionary keyed by `EventType` and exposes:

```csharp
public bool TryMap(IOutboxMessageView outboxMessage, out TOutput? mapped)
```

- [ ] **Step 3: Add notification consumer**

The consumer should:

```csharp
if (!registry.TryMap(outboxMessage, out var notificationMessage) || notificationMessage is null)
    return OutboxMessageConsumerResult.NotHandled();

await notificationProjector.ProjectAsync(notificationMessage, cancellationToken);
var publishResult = await notificationPublisher.PublishAsync(notificationMessage, cancellationToken);

return publishResult.IsSuccess
    ? OutboxMessageConsumerResult.Succeeded(publishResult.ProviderMessageId ?? string.Empty)
    : publishResult.IsTransient
        ? OutboxMessageConsumerResult.TransientFailure(publishResult.FailureReason ?? "Notification publish failed.")
        : OutboxMessageConsumerResult.PermanentFailure(publishResult.FailureReason ?? "Notification publish failed.");
```

- [ ] **Step 4: Add referral-operation consumer**

The consumer should:

```csharp
if (!registry.TryMap(outboxMessage, out var referralEvent) || referralEvent is null)
    return OutboxMessageConsumerResult.NotHandled();

await referralOperationProjector.ProjectAsync(referralEvent, cancellationToken);
return OutboxMessageConsumerResult.Succeeded();
```

- [ ] **Step 5: Split existing static mappers into registered mapper classes**

Use one mapper class per event type. Preserve the existing output values exactly.

- [ ] **Step 6: Register consumers and mappers**

In `AddInfrastructure(...)`, register:

```csharp
services.AddScoped<IOutboxMessageConsumer, ReferralOperationOutboxMessageConsumer>();
services.AddScoped<IOutboxMessageConsumer, NotificationOutboxMessageConsumer>();
services.AddScoped<IOutboxMessageMapper<NotificationMessage>, QuoteGeneratedNotificationMapper>();
services.AddScoped<IOutboxMessageMapper<ReferralOperationEvent>, QuoteGeneratedReferralOperationMapper>();
```

Repeat mapper registration for every event currently handled by the static mappers.

- [ ] **Step 7: Run dispatcher tests**

Run:

```powershell
dotnet test LIAnsureProtect.slnx --no-build --filter FullyQualifiedName~OutboxDispatcherTests
```

Expected: all `OutboxDispatcherTests` pass.

- [ ] **Step 8: Build before commit**

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
```

Expected: `0 Warning(s), 0 Error(s)`.

- [ ] **Step 9: Commit**

Run:

```powershell
git add -- src/LIAnsureProtect.Infrastructure/Persistence/Outbox src/LIAnsureProtect.Infrastructure/DependencyInjection.cs tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs
git commit -m "refactor(outbox): register integration event consumers"
```

## Task 5: Closeout Docs

**Files:**
- Create: `docs/dev/milestone-40-dispatcher-integration-event-decoupling-learnings.md`
- Modify: `docs/project-status.md`
- Modify: `docs/architecture/overview.md`
- Modify: `CHANGELOG.md`
- Modify: `README.md` if the milestone docs list needs the M40 note

- [ ] **Step 1: Add learning notes**

Capture:

```text
what changed;
why consumers and registries are enough for this slice;
why quote/rating/policy tables did not move;
how retry/order behavior stayed the same;
what remains for SNS/SQS in M43.
```

- [ ] **Step 2: Update project status**

Set:

```text
Latest closed milestone: Milestone 40 - Dispatcher Integration Event Decoupling.
Current milestone: Milestone 40 - Dispatcher Integration Event Decoupling, complete on branch ...
Recommended next milestone after M40: Milestone 41 - Observability.
```

- [ ] **Step 3: Update architecture overview**

Explain that M40 moved dispatcher side effects behind registered consumers and mapper registries.

- [ ] **Step 4: Build before commit**

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
```

Expected: `0 Warning(s), 0 Error(s)`.

- [ ] **Step 5: Commit**

Run:

```powershell
git add -- docs/dev/milestone-40-dispatcher-integration-event-decoupling-learnings.md docs/project-status.md docs/architecture/overview.md CHANGELOG.md README.md
git commit -m "docs: close dispatcher integration event decoupling milestone"
```

## Final Verification

Run:

```powershell
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build
dotnet tool restore
dotnet ef migrations has-pending-model-changes --context SubmissionDbContext    --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj                                                     --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext  --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj  --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
pwsh ./scripts/run-local-ci.ps1
```

Expected:

```text
build: 0 Warning(s), 0 Error(s)
tests: pass, with the PostgreSQL opt-in test skipped unless explicitly enabled
EF: no pending model changes for all three contexts
local CI: Local CI passed.
```
