# Milestone 37 - Underwriting Evidence (foundation + request/review carve) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the module-as-event-source foundation (a module-owned outbox + a source-agnostic, merge-ordered dispatcher) and carve the evidence **request + review** aggregates into the Underwriting module + `underwriting` schema; document-coupled use cases stay legacy and mutate the module request via one primitives-only inbound port.

**Architecture:** Strangler carve in three phases that each compile. Phase A builds the module outbox + generalizes the dispatcher across multiple `IOutboxSource`s (proven against the still-legacy outbox). Phase B carves the evidence request/review aggregates, routes document-coupled handlers through `IEvidenceRequestWriter`, and drops the cross-context FKs. Phase C deletes legacy evidence request/review code, runs the drop migration, and ships docs + PR. No new `DbContext` (the module outbox is a table in `UnderwritingDbContext`), so scripts/guard/CI are unchanged.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10 + Npgsql, MediatR, xUnit + Moq, PostgreSQL (`underwriting` schema), the M33/M36 outbox-dispatcher → projector seam.

---

## Conventions for this plan

- **Moved files are transcribed verbatim** with only the enumerated edits (namespace, dropped FK/property, a Quoting type → string at a seam). A move is precise, not a placeholder.
- Build: `dotnet build LIAnsureProtect.slnx --no-restore` (expect `0 Warning(s) / 0 Error(s)`).
- Test: `dotnet test LIAnsureProtect.slnx --no-build` unless a focused `--filter` is given.
- Pending-model check (all three contexts) — same three commands as the M36 plan's conventions block (Submission / Notifications / Underwriting).
- **No AI attribution** on any commit (no `Co-Authored-By: Claude`, `🤖`, `Generated with`). Plain messages only.
- Module project short paths: Domain `src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Domain`; Application `…Underwriting.Application`; Infrastructure `…Underwriting.Infrastructure`. Platform: `src/Platform/LIAnsureProtect.Platform`; ports `src/Platform/LIAnsureProtect.Platform.Abstractions`.

---

# PHASE A — Module outbox + multi-source dispatch foundation (legacy behaviour preserved; tests stay green)

## Task A1: Outbox dispatch abstractions in Platform.Abstractions

**Files:**
- Create: `src/Platform/LIAnsureProtect.Platform.Abstractions/Outbox/IOutboxSource.cs`
- Create: `src/Platform/LIAnsureProtect.Platform.Abstractions/Outbox/IOutboxMessageView.cs`

- [ ] **Step 1: Write the abstractions**

`IOutboxMessageView.cs`:
```csharp
namespace LIAnsureProtect.Platform.Abstractions.Outbox;

/// <summary>
/// A source-neutral view of one pending outbox row the dispatcher can map, publish, and mark — so the
/// dispatcher works the same over the legacy (Submission) outbox and any module outbox.
/// </summary>
public interface IOutboxMessageView
{
    Guid Id { get; }
    string Type { get; }
    string Payload { get; }
    DateTime CreatedAtUtc { get; }

    void MarkProcessed(DateTime processedAtUtc);
    void MarkPublishSucceeded(DateTime processedAtUtc, string providerMessageId);
    void MarkPublishFailed(DateTime attemptedAtUtc, string failureReason, DateTime? nextAttemptAtUtc, bool exhausted);
    int PublishAttemptCount { get; }
}
```

`IOutboxSource.cs`:
```csharp
namespace LIAnsureProtect.Platform.Abstractions.Outbox;

/// <summary>
/// One outbox the dispatcher drains. Each bounded context that emits events exposes its outbox as a
/// source; the dispatcher merges all sources' pending messages and processes them in CreatedAtUtc order.
/// </summary>
public interface IOutboxSource
{
    Task<IReadOnlyList<IOutboxMessageView>> GetPendingAsync(int batchSize, DateTime nowUtc, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Platform/LIAnsureProtect.Platform.Abstractions/Outbox
git commit -m "feat(platform): add source-agnostic outbox dispatch abstractions"
```

## Task A2: Reusable module outbox message in Platform

**Files:**
- Create: `src/Platform/LIAnsureProtect.Platform/Outbox/ModuleOutboxMessage.cs`
- Create: `src/Platform/LIAnsureProtect.Platform/Outbox/ModuleOutboxMessageConfiguration.cs`

- [ ] **Step 1: Write `ModuleOutboxMessage`**

Reproduce the legacy `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxMessage.cs` shape exactly (same fields, `Mark*` methods, and `FromDomainEvent` factory), but in Platform and implementing `IOutboxMessageView`:

```csharp
using System.Text.Json;
using LIAnsureProtect.Platform.Abstractions.DomainEvents;
using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Platform.Outbox;

/// <summary>
/// Reusable transactional-outbox row for any module context (mirrors the legacy OutboxMessage). Captured
/// in the same transaction as the business change by <c>ModuleDbContext.CaptureDomainEventsAsync</c>.
/// </summary>
public sealed class ModuleOutboxMessage : IOutboxMessageView
{
    private ModuleOutboxMessage(Guid id, string type, string payload, DateTime occurredAtUtc, DateTime createdAtUtc)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAtUtc = occurredAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    private ModuleOutboxMessage()
    {
        Type = string.Empty;
        Payload = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Type { get; private set; }
    public string Payload { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public string? Error { get; private set; }
    public int PublishAttemptCount { get; private set; }
    public DateTime? LastPublishAttemptAtUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }

    public void MarkProcessed(DateTime processedAtUtc)
    {
        ProcessedAtUtc = processedAtUtc;
        NextAttemptAtUtc = null;
        FailedAtUtc = null;
        Error = null;
    }

    public void MarkPublishSucceeded(DateTime processedAtUtc, string providerMessageId)
    {
        if (string.IsNullOrWhiteSpace(providerMessageId))
            throw new ArgumentException("Provider message id is required.", nameof(providerMessageId));

        PublishAttemptCount++;
        LastPublishAttemptAtUtc = processedAtUtc;
        ProviderMessageId = providerMessageId.Trim();
        MarkProcessed(processedAtUtc);
    }

    public void MarkPublishFailed(DateTime attemptedAtUtc, string failureReason, DateTime? nextAttemptAtUtc, bool exhausted)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("Failure reason is required.", nameof(failureReason));

        PublishAttemptCount++;
        LastPublishAttemptAtUtc = attemptedAtUtc;
        NextAttemptAtUtc = exhausted ? null : nextAttemptAtUtc;
        FailedAtUtc = exhausted ? attemptedAtUtc : null;
        Error = failureReason.Trim();
    }

    public static ModuleOutboxMessage FromDomainEvent(IDomainEvent domainEvent, DateTime createdAtUtc)
    {
        var eventType = domainEvent.GetType();
        return new ModuleOutboxMessage(
            Guid.NewGuid(),
            eventType.Name,
            JsonSerializer.Serialize(domainEvent, eventType),
            domainEvent.OccurredAtUtc,
            createdAtUtc);
    }
}
```

- [ ] **Step 2: Write the reusable EF config** (a module applies it in its own schema)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Platform.Outbox;

/// <summary>Maps <see cref="ModuleOutboxMessage"/> to an <c>outbox_messages</c> table in the module's schema.</summary>
public sealed class ModuleOutboxMessageConfiguration : IEntityTypeConfiguration<ModuleOutboxMessage>
{
    public void Configure(EntityTypeBuilder<ModuleOutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(message => message.Type).HasColumnName("type").HasMaxLength(200).IsRequired();
        builder.Property(message => message.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(message => message.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(message => message.ProcessedAtUtc).HasColumnName("processed_at_utc");
        builder.Property(message => message.Error).HasColumnName("error").HasColumnType("text");
        builder.Property(message => message.PublishAttemptCount).HasColumnName("publish_attempt_count").IsRequired();
        builder.Property(message => message.LastPublishAttemptAtUtc).HasColumnName("last_publish_attempt_at_utc");
        builder.Property(message => message.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(message => message.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(200);
        builder.Property(message => message.FailedAtUtc).HasColumnName("failed_at_utc");
        builder.HasIndex(message => new { message.ProcessedAtUtc, message.FailedAtUtc, message.NextAttemptAtUtc, message.CreatedAtUtc })
            .HasDatabaseName("ix_outbox_messages_pending");
    }
}
```

> Cross-check the legacy `OutboxMessageConfiguration` (under `…Infrastructure/Persistence/Configurations` or Outbox) for the exact legacy column names/types and match them so the rows are shaped identically; adjust the column types above if the legacy config differs.

- [ ] **Step 3: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Platform/LIAnsureProtect.Platform/Outbox
git commit -m "feat(platform): add reusable module outbox message + config"
```

## Task A3: Make UnderwritingDbContext capture domain events to its own outbox

**Files:**
- Modify: `…Underwriting.Infrastructure/Persistence/UnderwritingDbContext.cs`

- [ ] **Step 1: Add the outbox DbSet, apply the config, override capture**

```csharp
    public DbSet<ModuleOutboxMessage> OutboxMessages => Set<ModuleOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UnderwritingDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new ModuleOutboxMessageConfiguration());
    }

    protected override async Task CaptureDomainEventsAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken)
    {
        var createdAtUtc = DateTime.UtcNow;
        var outboxMessages = domainEvents
            .Select(domainEvent => ModuleOutboxMessage.FromDomainEvent(domainEvent, createdAtUtc))
            .ToList();
        await OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
    }
```

Add `using LIAnsureProtect.Platform.Abstractions.DomainEvents;` and `using LIAnsureProtect.Platform.Outbox;`. (The base `ModuleDbContext.SaveChangesAsync` already calls `CaptureDomainEventsAsync` before `base.SaveChangesAsync`, inside the same transaction.)

- [ ] **Step 2: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/Persistence/UnderwritingDbContext.cs
git commit -m "feat(underwriting): capture module domain events to its own outbox"
```

## Task A4: Add the CreateUnderwritingOutbox migration

**Files:** generated migration under `…Underwriting.Infrastructure/Migrations/`.

- [ ] **Step 1: Generate**

```
dotnet ef migrations add CreateUnderwritingOutbox --context UnderwritingDbContext --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
```
Confirm it creates `underwriting.outbox_messages` only.

- [ ] **Step 2: Verify no pending model changes for UnderwritingDbContext** (the `has-pending-model-changes --context UnderwritingDbContext …` command). Expect "No changes".

- [ ] **Step 3: Commit**

```bash
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/Migrations
git commit -m "feat(underwriting): add module outbox migration"
```

## Task A5: Outbox sources (legacy + module)

**Files:**
- Create: `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/SubmissionOutboxSource.cs`
- Create: `…Underwriting.Infrastructure/Persistence/UnderwritingOutboxSource.cs`

- [ ] **Step 1: Legacy source** (wraps the existing `OutboxMessage`, which already has the `Mark*` methods)

```csharp
using LIAnsureProtect.Platform.Abstractions.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

/// <summary>The legacy Submission/Quoting outbox exposed as an <see cref="IOutboxSource"/>.</summary>
public sealed class SubmissionOutboxSource(SubmissionDbContext dbContext) : IOutboxSource
{
    public async Task<IReadOnlyList<IOutboxMessageView>> GetPendingAsync(int batchSize, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var pending = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAtUtc == null
                && message.FailedAtUtc == null
                && (message.NextAttemptAtUtc == null || message.NextAttemptAtUtc <= nowUtc))
            .OrderBy(message => message.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        return pending;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
```

> This requires the legacy `OutboxMessage` to implement `IOutboxMessageView` (it already has `Id`/`Type`/`Payload`/`CreatedAtUtc`/`PublishAttemptCount` + the three `Mark*` methods). In this step, add `: IOutboxMessageView` to the legacy `OutboxMessage` class declaration and `using LIAnsureProtect.Platform.Abstractions.Outbox;` — no member changes needed (the signatures already match A1). Verify by building.

- [ ] **Step 2: Module source**

```csharp
using LIAnsureProtect.Platform.Abstractions.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

/// <summary>The Underwriting module outbox exposed as an <see cref="IOutboxSource"/>.</summary>
public sealed class UnderwritingOutboxSource(UnderwritingDbContext dbContext) : IOutboxSource
{
    public async Task<IReadOnlyList<IOutboxMessageView>> GetPendingAsync(int batchSize, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var pending = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAtUtc == null
                && message.FailedAtUtc == null
                && (message.NextAttemptAtUtc == null || message.NextAttemptAtUtc <= nowUtc))
            .OrderBy(message => message.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        return pending;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
```

- [ ] **Step 3: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/LIAnsureProtect.Infrastructure/Persistence/Outbox/SubmissionOutboxSource.cs src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxMessage.cs src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/Persistence/UnderwritingOutboxSource.cs
git commit -m "feat(outbox): expose legacy and module outboxes as IOutboxSource"
```

## Task A6: Generalize mappers + dispatcher to merge-ordered multi-source

**Files:**
- Modify: `…Infrastructure/Persistence/Outbox/OutboxNotificationMapper.cs`
- Modify: `…Infrastructure/Persistence/Outbox/OutboxReferralOperationMapper.cs`
- Modify: `…Infrastructure/Persistence/Outbox/OutboxDispatcher.cs`
- Modify: `src/LIAnsureProtect.Infrastructure/DependencyInjection.cs`
- Modify: `…Underwriting.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Mappers take the view, not the concrete message**

In both mappers change the public entry `TryMap(OutboxMessage outboxMessage)` → `TryMap(IOutboxMessageView outboxMessage)` and the private `Deserialize<T>` to read `outboxMessage.Payload`/`outboxMessage.Id` from the interface (those members exist on `IOutboxMessageView`). No mapping logic changes. Add `using LIAnsureProtect.Platform.Abstractions.Outbox;`.

- [ ] **Step 2: Dispatcher drains all sources in merge order**

Rewrite `OutboxDispatcher` to inject `IEnumerable<IOutboxSource>` instead of `SubmissionDbContext`, collect each source's pending batch, tag each message with its source, merge-sort by `CreatedAtUtc`, then run the existing per-message body (referral projection → notification map/project/publish → mark), saving each source that had activity:

```csharp
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

public sealed class OutboxDispatcher(
    IEnumerable<IOutboxSource> sources,
    INotificationProjector notificationProjector,
    INotificationPublisher notificationPublisher,
    IReferralOperationProjector referralOperationProjector) : IOutboxDispatcher
{
    private const int BatchSize = 20;
    private const int MaxPublishAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    public async Task<int> DispatchPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        var sourceList = sources.ToList();
        var pending = new List<(IOutboxMessageView Message, IOutboxSource Source)>();
        foreach (var source in sourceList)
        {
            foreach (var message in await source.GetPendingAsync(BatchSize, nowUtc, cancellationToken))
                pending.Add((message, source));
        }

        if (pending.Count == 0)
            return 0;

        var ordered = pending.OrderBy(item => item.Message.CreatedAtUtc).ToList();
        var touchedSources = new HashSet<IOutboxSource>();
        var processedCount = 0;

        foreach (var (message, source) in ordered)
        {
            touchedSources.Add(source);

            var referralEvent = OutboxReferralOperationMapper.TryMap(message);
            if (referralEvent is not null)
                await referralOperationProjector.ProjectAsync(referralEvent, cancellationToken);

            var notificationMessage = OutboxNotificationMapper.TryMap(message);
            if (notificationMessage is null)
            {
                message.MarkProcessed(nowUtc);
                processedCount++;
                continue;
            }

            await notificationProjector.ProjectAsync(notificationMessage, cancellationToken);
            var publishResult = await notificationPublisher.PublishAsync(notificationMessage, cancellationToken);

            if (publishResult.IsSuccess)
            {
                message.MarkPublishSucceeded(nowUtc, publishResult.ProviderMessageId ?? string.Empty);
                processedCount++;
                continue;
            }

            var nextAttemptNumber = message.PublishAttemptCount + 1;
            var exhausted = !publishResult.IsTransient || nextAttemptNumber >= MaxPublishAttempts;
            message.MarkPublishFailed(
                nowUtc,
                publishResult.FailureReason ?? "Notification publish failed.",
                exhausted ? null : nowUtc.Add(RetryDelay),
                exhausted);
        }

        foreach (var source in touchedSources)
            await source.SaveChangesAsync(cancellationToken);

        return processedCount;
    }
}
```

- [ ] **Step 3: Register the sources**

In legacy `AddInfrastructure`, replace nothing about the dispatcher registration but add the legacy source:
```csharp
        services.AddScoped<IOutboxSource, SubmissionOutboxSource>();
```
(Add `using LIAnsureProtect.Platform.Abstractions.Outbox;`.) In `AddUnderwritingModule`, register the module source:
```csharp
        services.AddScoped<IOutboxSource, UnderwritingOutboxSource>();
```
Both register against the same `IOutboxSource` contract; the dispatcher receives both via `IEnumerable<IOutboxSource>`.

- [ ] **Step 4: Build**

Run: `dotnet build LIAnsureProtect.slnx --no-restore` → 0/0.

- [ ] **Step 5: Update existing dispatcher tests for the new constructor**

`tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs` constructs `OutboxDispatcher` directly. Wrap the existing `SubmissionDbContext` in a `SubmissionOutboxSource` and pass `new IOutboxSource[] { new SubmissionOutboxSource(dbContext) }` as the first arg (keep the existing notification projector/publisher + referral projector args). Existing assertions stay.

- [ ] **Step 6: Run dispatcher tests; commit**

```bash
dotnet test LIAnsureProtect.slnx --filter "FullyQualifiedName~OutboxDispatcher"
git add src/LIAnsureProtect.Infrastructure src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/DependencyInjection.cs tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs
git commit -m "feat(outbox): drain all outbox sources in merge-ordered dispatch"
```

## Task A7: Multi-source + ordering dispatcher tests

**Files:**
- Modify: `tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs`

- [ ] **Step 1: Write the tests**

Using the existing two-connection SQLite pattern plus an `UnderwritingDbContext` over its own SQLite connection (mirror `ReferralOperationProjectorTests`):

```csharp
[Fact]
public async Task DispatchPendingMessagesAsync_Drains_Both_Sources()
{
    // legacy outbox: a QuoteGenerated (Quoted) message; module outbox: an evidence-created message
    // (build a ModuleOutboxMessage.FromDomainEvent for a module evidence event once Phase B exists —
    // for Phase A, assert the dispatcher accepts an IEnumerable with both sources and processes the
    // legacy message; extend with the module evidence message in Phase B Task B-dispatch).
}

[Fact]
public async Task DispatchPendingMessagesAsync_Processes_Messages_In_CreatedAtUtc_Order_Across_Sources()
{
    // legacy decision message CreatedAtUtc = T+1; module evidence message CreatedAtUtc = T;
    // assert the module (earlier) message is processed before the legacy (later) one
    // (capture processing order via a recording projector/publisher).
}
```

> The fully-wired cross-source evidence-vs-decision ordering assertion lands in Phase B once a module evidence event type exists; in Phase A assert ordering with two legacy-shaped messages placed in two sources (a second `SubmissionOutboxSource` over a separate context is sufficient to prove the merge-sort).

- [ ] **Step 2: Run; commit**

```bash
dotnet test LIAnsureProtect.slnx --filter "FullyQualifiedName~OutboxDispatcher"
git add tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs
git commit -m "test(outbox): cover multi-source draining and merge ordering"
```

---

# PHASE B — Evidence request/review carve (strangler; build green per commit, full suite green at the end)

## Task B1: Move the evidence enums + events into the module Domain

**Files:**
- Create under `…Underwriting.Domain/Evidence/`: `EvidenceRequestCategory.cs`, `EvidenceRequestStatus.cs`, `EvidenceReviewDecisionStatus.cs`, `QuoteEvidenceRequestDomainEvents.cs`
- Legacy copies stay until Phase C.

- [ ] **Step 1:** Copy each legacy file from `src/LIAnsureProtect.Domain/Quotes/` verbatim, changing only the namespace to `LIAnsureProtect.Modules.Underwriting.Domain.Evidence`. For `QuoteEvidenceRequestDomainEvents.cs`, the events reference `EvidenceRequestCategory`/`EvidenceReviewDecisionStatus` (now same namespace) and `IDomainEvent` (`using LIAnsureProtect.Platform.Abstractions.DomainEvents;` — already used). Do NOT copy `EvidenceDocumentScanStatus` (document enum stays legacy for M38).

- [ ] **Step 2: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Domain/Evidence
git commit -m "feat(underwriting): add evidence enums and events to module domain"
```

## Task B2: Move the request + review aggregates into the module Domain

**Files:**
- Create: `…Underwriting.Domain/Evidence/QuoteEvidenceRequest.cs`, `…/QuoteEvidenceRequestReview.cs`

- [ ] **Step 1:** Copy `QuoteEvidenceRequest.cs` verbatim with edits: namespace → `…Domain.Evidence`; **delete the `QuoteReferralOperationId` property** and its constructor assignment and its `Create(...)` parameter (the M36 vestige retires — `Create` no longer takes `quoteReferralOperationId` and no longer validates it). Copy `QuoteEvidenceRequestReview.cs` verbatim with the namespace change only (its `Record(...)` takes the request + counts; the enums it uses are now same-namespace). Read both legacy files in full first and reproduce the bodies exactly.

- [ ] **Step 2: Build & move the aggregate unit tests**

Move `tests/LIAnsureProtect.UnitTests/Quotes/QuoteEvidenceRequestTests.cs` (and any `QuoteEvidenceRequestReview` tests) to `tests/LIAnsureProtect.UnitTests/Modules/Underwriting/Evidence/`, changing the `using` to `LIAnsureProtect.Modules.Underwriting.Domain.Evidence` and removing any `Create(...)` argument for the deleted `quoteReferralOperationId`. `git rm` the legacy test files.

- [ ] **Step 3: Run moved tests; commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
dotnet test LIAnsureProtect.slnx --no-build --filter "FullyQualifiedName~QuoteEvidenceRequestTests"
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Domain/Evidence tests/LIAnsureProtect.UnitTests/Modules/Underwriting/Evidence
git rm tests/LIAnsureProtect.UnitTests/Quotes/QuoteEvidenceRequestTests.cs
git commit -m "feat(underwriting): add evidence request and review aggregates to module domain"
```

## Task B3: Module evidence ports (repository, reader, writer)

**Files:**
- Create under `…Underwriting.Application/Evidence/`: `IEvidenceRequestRepository.cs`, `IEvidenceRequestsReader.cs` (+ result records), `IEvidenceRequestWriter.cs`

- [ ] **Step 1: Repository port**

```csharp
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence;

public interface IEvidenceRequestRepository
{
    Task AddAsync(QuoteEvidenceRequest evidenceRequest, CancellationToken cancellationToken);
    Task AddReviewAsync(QuoteEvidenceRequestReview review, CancellationToken cancellationToken);
    Task<QuoteEvidenceRequest?> GetForUnderwritingAsync(Guid quoteId, Guid evidenceRequestId, CancellationToken cancellationToken);
    Task<QuoteEvidenceRequest?> GetForOwnerAsync(Guid evidenceRequestId, string ownerUserId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Reader port + result records**

```csharp
namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence;

public interface IEvidenceRequestsReader
{
    Task<IReadOnlyCollection<EvidenceRequestOwnerItem>> GetOwnerRequestsAsync(string ownerUserId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EvidenceRequestSummaryItem>> GetSummariesAsync(IReadOnlyCollection<Guid> quoteIds, CancellationToken cancellationToken);
}

// Field set mirrors the existing owner-list and queue-summary result DTOs (copy the exact members from
// the legacy ListOwnerEvidenceRequestsResult and QuoteReferralEvidenceSummaryResult when implementing).
public sealed record EvidenceRequestOwnerItem(/* exact fields from legacy owner-list result */);
public sealed record EvidenceRequestSummaryItem(/* exact per-quote summary fields */);
```

> When implementing, open the legacy `ListOwnerEvidenceRequestsResult` and the
> `QuoteReferralEvidenceSummaryResult` (in `ListQuoteReferralsResult.cs`) and reproduce their field sets
> exactly so the API contract is unchanged.

- [ ] **Step 3: Writer (inbound) port — primitives only**

```csharp
namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence;

/// <summary>
/// Inbound port the legacy document-coupled handlers call to apply a request-state change to the
/// module-owned evidence request (documents + clean-document gate stay legacy in M37). All parameters
/// are primitives; the port validates ownership/state and maps to the aggregate internally. Returns a
/// flat snapshot the caller assembles into its response, or null when the request is not found/eligible.
/// </summary>
public interface IEvidenceRequestWriter
{
    Task<EvidenceRequestSnapshot?> RecordResponseAsync(Guid evidenceRequestId, string ownerUserId, string respondentName, string respondentTitle, string responseText, string? attachmentFileName, string? attachmentContentType, long? attachmentSizeBytes, DateTime respondedAtUtc, CancellationToken cancellationToken);
    Task<EvidenceRequestSnapshot?> RecordSupplementalResponseAsync(Guid evidenceRequestId, string ownerUserId, string respondentName, string respondentTitle, string responseText, string? attachmentFileName, string? attachmentContentType, long? attachmentSizeBytes, DateTime respondedAtUtc, CancellationToken cancellationToken);
    Task<EvidenceRequestSnapshot?> AcceptAsync(Guid quoteId, Guid evidenceRequestId, string reviewedByUserId, string reason, int documentCount, int cleanDocumentCount, DateTime reviewedAtUtc, CancellationToken cancellationToken);
    Task<EvidenceRequestSnapshot?> RecordReviewDecisionAsync(Guid quoteId, Guid evidenceRequestId, string decision, string reason, string? remediationGuidance, string reviewedByUserId, int documentCount, int cleanDocumentCount, DateTime reviewedAtUtc, CancellationToken cancellationToken);
}

// Flat snapshot of request state for the legacy caller to project into its existing response DTO.
public sealed record EvidenceRequestSnapshot(/* the request fields the legacy QuoteEvidenceRequestResult exposes */);
```

> `decision` is a string (`"Insufficient"`/`"NeedsClarification"`) — the legacy handler parses its enum,
> passes the string; the writer maps to the module `EvidenceReviewDecisionStatus`. Reproduce
> `EvidenceRequestSnapshot`'s fields from the legacy `QuoteEvidenceRequestResult`.

- [ ] **Step 4: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Application/Evidence
git commit -m "feat(underwriting): add evidence repository, reader, and writer ports"
```

## Task B4: Move the document-free commands + owner-list query into the module

**Files:**
- Create under `…Underwriting.Application/Evidence/`: `Commands/ManageEvidenceRequests/` (Create, Cancel, FollowUp + results), `Queries/ListOwnerEvidenceRequests/`

- [ ] **Step 1:** From the legacy `QuoteEvidenceRequestCommands.cs`, reproduce ONLY the `CreateQuoteEvidenceRequest`, `CancelQuoteEvidenceRequest`, `FollowUpQuoteEvidenceRequest` commands+handlers, the `ListOwnerEvidenceRequestsQuery`+handler, and the result records they need. Apply: namespace → `…Application.Evidence...`; constructor deps `(IQuoteRepository, IUnitOfWork, ICurrentUser)` → `(IEvidenceRequestRepository evidence, ICurrentUser currentUser)` for create/cancel/follow-up (they don't touch documents), and `(IEvidenceRequestsReader reader, ICurrentUser currentUser)` for the owner-list query; repo calls → the module repository (`GetForUnderwritingAsync`/`AddAsync`/`SaveChangesAsync`); the create handler reads the quote's `Referred` state via the existing `IUnderwritingQuoteContextReader` (M35 port — inject it) instead of `IQuoteRepository.GetForUnderwritingReviewAsync`, guarding `Status == "Referred"` (string). The create handler no longer needs an operation id (the `Create` parameter was removed in B2).

> Read the legacy create/cancel/follow-up handlers in full and reproduce their bodies, substituting the dependencies above. Keep validation behaviour identical.

- [ ] **Step 2: Build & commit** (legacy command file still compiles — duplication intended until Phase C)

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Application/Evidence/Commands src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Application/Evidence/Queries
git commit -m "feat(underwriting): add document-free evidence commands and owner query to module"
```

## Task B5: Module evidence Infrastructure (configs, repository, reader, writer, migration)

**Files:**
- Create under `…Underwriting.Infrastructure/Persistence/`: `QuoteEvidenceRequestConfiguration.cs`, `QuoteEvidenceRequestReviewConfiguration.cs`, `EfEvidenceRequestRepository.cs`, `EvidenceRequestsReader.cs`, `EvidenceRequestWriter.cs`
- Generated migration `CreateEvidenceRequests`

- [ ] **Step 1: EF configs** — copy the legacy `QuoteEvidenceRequestConfiguration` + `QuoteEvidenceRequestReviewConfiguration` into the module, changing namespace to `LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence` and `using LIAnsureProtect.Domain.Quotes;` → `using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;`. **Delete** the `HasOne<Quote>()` and `HasOne<Submission>()` cross-context FK blocks and the `QuoteReferralOperationId` property mapping (the property was removed in B2). Schema comes from `UnderwritingDbContext`. Keep `builder.Ignore(request => request.DomainEvents)`.

- [ ] **Step 2: Repository** — `EfEvidenceRequestRepository(UnderwritingDbContext dbContext)` implementing the five port methods over `dbContext.Set<QuoteEvidenceRequest>()`/`Set<QuoteEvidenceRequestReview>()` (no-tracking off for the for-update gets; AddAsync/AddReviewAsync; SaveChangesAsync). Mirror `EfReferralOperationRepository`.

- [ ] **Step 3: Reader** — `EvidenceRequestsReader(UnderwritingDbContext dbContext)` implementing `GetOwnerRequestsAsync` (no-tracking, owner-scoped) and `GetSummariesAsync` (per-quote summary). Reproduce the exact projection logic from the legacy `ListOwnerEvidenceRequestsQueryHandler` and from `CreateEvidenceSummary(...)` in `ListQuoteReferralsQueryHandler` so output is identical.

- [ ] **Step 4: Writer** — `EvidenceRequestWriter(UnderwritingDbContext dbContext)` implementing the four inbound methods: load the request (owner- or underwriting-scoped), call the matching aggregate method (`RecordResponse`/`Accept`/`RecordReviewDecision`), for accept/review also build + add the `QuoteEvidenceRequestReview` audit from the passed counts, `SaveChangesAsync`, return an `EvidenceRequestSnapshot`. Map the `decision` string to `EvidenceReviewDecisionStatus` internally.

- [ ] **Step 5: Migration**

```
dotnet ef migrations add CreateEvidenceRequests --context UnderwritingDbContext --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
```
Confirm it creates `underwriting.quote_evidence_requests` + `underwriting.quote_evidence_request_reviews` with no cross-context FK. Verify `has-pending-model-changes --context UnderwritingDbContext` is clean.

- [ ] **Step 6: Register + build + commit**

In `AddUnderwritingModule`: `AddScoped<IEvidenceRequestRepository, EfEvidenceRequestRepository>()`, `IEvidenceRequestsReader`, `IEvidenceRequestWriter`.
```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure
git commit -m "feat(underwriting): implement evidence persistence, reader, writer, migration"
```

## Task B6: Add a module migration test

- [ ] Mirror `CreateReferralOperationsMigrationTests` for the evidence tables + the module outbox in `underwriting`; run it; commit.

## Task B7: Cut over — controllers, legacy handlers → writer port, reads → reader, dispatcher ordering test

**Files:**
- Modify: `EvidenceRequestsController.cs`, `UnderwritingQuoteReferralsController.cs`
- Modify: legacy `QuoteEvidenceRequestCommands.cs` (respond/replacement/accept/review/downloads keep; call the writer port; remove the moved create/cancel/follow-up + owner-list)
- Modify: `ListQuoteReferralsQueryHandler.cs` (evidence summary via reader)
- Modify: `IQuoteRepository`/`EfCoreQuoteRepository` (remove the moved request/review methods; keep the document methods)
- Modify: `ProjectReferenceBoundaryTests.cs` (add the `Infrastructure → Modules.Underwriting.Domain` edge for the mappers)

- [ ] **Step 1:** Point the controllers at the module commands/queries for create/cancel/follow-up/owner-list; leave respond/accept/review/download actions dispatching the legacy handlers.
- [ ] **Step 2:** In the legacy respond/replacement/accept/review handlers, replace the request load+mutate+save with a call to `IEvidenceRequestWriter` (inject it), keeping all document store/scan/gate/count logic; assemble the response from the returned snapshot + legacy documents. Remove the create/cancel/follow-up/owner-list commands+handlers from the legacy file.
- [ ] **Step 3:** `ListQuoteReferralsQueryHandler` uses `IEvidenceRequestsReader.GetSummariesAsync`.
- [ ] **Step 4:** Remove the moved methods (`AddEvidenceRequestAsync`, `AddEvidenceRequestReviewAsync`, `GetEvidenceRequestForUnderwritingAsync`, `GetEvidenceRequestForOwnerAsync`, `ListEvidenceRequestsForQuotesAsync`, `ListEvidenceRequestsForOwnerAsync`) from `IQuoteRepository`/`EfCoreQuoteRepository`; **keep** the document methods (`AddEvidenceDocumentsAsync`, `ListEvidenceDocumentsForRequestsAsync`, `GetEvidenceDocumentForOwnerAsync`, `GetEvidenceDocumentForUnderwritingAsync`).
- [ ] **Step 5:** Update `ProjectReferenceBoundaryTests` Infrastructure row to add `LIAnsureProtect.Modules.Underwriting.Domain` (the mappers now deserialize module evidence events). Update legacy `Application` row only if a new module reference was added there.
- [ ] **Step 6:** Build 0/0; commit. (Integration suite may be red until B8 reworks tests — gate is build + architecture test.)

## Task B8: Rework evidence integration tests for the two-source + writer wiring

- [ ] Pump the dispatcher where eventual consistency applies (evidence events now flow from the module outbox); rework `EvidenceDocumentEndpointTests` + the evidence tests in `UnderwritingReferralEndpointTests` so respond/accept/review exercise the `IEvidenceRequestWriter` seam (documents legacy, request/review module). Add the cross-source ordering integration test (evidence module event before legacy decision event). Do not weaken assertions. Full suite green. Commit.

---

# PHASE C — Delete legacy evidence request/review & finish

## Task C1: Remove the legacy request/review aggregates, enums, events, configs

- [ ] `git rm` the legacy `Domain/Quotes` evidence request/review files + the moved enums (`EvidenceRequestCategory`, `EvidenceRequestStatus`, `EvidenceReviewDecisionStatus`) + `QuoteEvidenceRequestDomainEvents.cs` + the two legacy EF configs. **Keep** `EvidenceDocumentScanStatus`, `QuoteEvidenceDocument`, and the document configs (documents stay legacy for M38). Fix any remaining references (the document code references `EvidenceRequestStatus`? if so, it now uses the module enum via the writer seam — verify and resolve, keeping legacy document code compiling). Build 0/0; commit.

## Task C2: Drop migration + document→request FK

- [ ] In the legacy `QuoteEvidenceDocumentConfiguration`, drop the `HasOne<QuoteEvidenceRequest>()` FK (documents reference the request by id only now). Generate `DropEvidenceRequests` on `SubmissionDbContext` (drops `quote_evidence_requests` + `quote_evidence_request_reviews`, taking the vestigial `quote_referral_operation_id` column with them, and drops the document→request FK). Verify all three contexts pending-clean. Commit.

## Task C3: Full verification

- [ ] Build 0/0; `dotnet test` full green (1 PostgreSQL opt-in skip); pending-model clean ×3. Commit any fixups.

## Task C4: Docs

- [ ] Learnings doc (`docs/dev/milestone-37-underwriting-evidence-learnings.md`); update `project-status.md` (M37 status/verification + M38 handoff: evidence documents), `CHANGELOG.md`, `README.md`, `docs/architecture/overview.md`. Commit.

## Task C5: Full local CI + PR

- [ ] `pwsh ./scripts/run-local-ci.ps1` green; push; `gh pr create --base main` with a clean no-attribution body. Squash-merge when CI + Claude review pass; re-sync; seed M38.

---

## Self-Review

**Spec coverage:**
- Module outbox + capture → A2, A3, A4. ✓
- Source-agnostic, merge-ordered dispatch → A1, A5, A6 (+ ordering tests A7, B8). ✓
- Document-coupling boundary (move create/cancel/follow-up + reads; respond/accept/review legacy via writer) → B4, B7. ✓
- Inbound writer port (primitives, counts) → B3, B5, B7. ✓
- Reads via module reader → B3, B5, B7. ✓
- Drop evidence→quotes/submissions FKs, document→request FK, vestigial column/property → B2, B5, C1, C2. ✓
- Migrations (CreateUnderwritingOutbox, CreateEvidenceRequests, DropEvidenceRequests) → A4, B5, C2. ✓
- Ratchet edge Infrastructure → Modules.Underwriting.Domain → B7. ✓
- Centralized mapping stays legacy → A6, B7. ✓
- Documents deferred to M38 → C1 (kept), C4 handoff. ✓
- Verification + CI + PR → C3, C5. ✓

**Placeholder scan:** The result/snapshot record field sets are intentionally "reproduce from the named legacy type" with the exact source named — not vague TODOs; the implementer copies a concrete existing type. All commands/migrations are exact.

**Type consistency:** `IOutboxSource`/`IOutboxMessageView` (A1) used in A5/A6; `ModuleOutboxMessage.FromDomainEvent` (A2) used in A3; `IEvidenceRequestWriter` methods (B3) called in B7; `IEvidenceRequestsReader.GetSummariesAsync`/`GetOwnerRequestsAsync` (B3) used in B5/B7; repository methods (B3) used in B4/B5. Consistent.
