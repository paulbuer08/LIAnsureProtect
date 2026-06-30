# Milestone 36 - Underwriting Referral Operations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Carve the `QuoteReferralOperation` aggregate (referral queue/SLA, work notes, follow-up tasks, timeline) out of legacy `Quotes` into the Underwriting module + `underwriting` schema, with create/close/evidence-projection driven by domain events through the outbox dispatcher → a new module projector.

**Architecture:** Strangler carve in three phases that each compile: (Phase A) build the new module home fully alongside the legacy aggregate; (Phase B) wire the event projector + module read port + module write commands and cut every caller over; (Phase C) delete the legacy aggregate/repository methods/tables and update docs. Cross-context writes go through existing domain events (`QuoteGeneratedDomainEvent`, `QuoteUnderwritingDecisionRecordedDomainEvent`, the six `QuoteEvidenceRequest*DomainEvent`s); reads stay legacy behind one module read port. No new `DbContext` (tables join `UnderwritingDbContext`), so scripts/guard/CI are unchanged.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10 + Npgsql, MediatR, xUnit + Moq, PostgreSQL (`underwriting` schema), the M33 outbox-dispatcher → module-projector seam.

---

## Conventions for this plan

- **Moved files are transcribed verbatim** with only the enumerated edits (namespace, dropped cross-context FK, Quoting-enum parameter → `string`). A move is a precise operation, not a placeholder — do not paraphrase the body.
- Build command: `dotnet build LIAnsureProtect.slnx --no-restore` (expect `0 Warning(s) / 0 Error(s)`).
- Test command: `dotnet test LIAnsureProtect.slnx --no-build` unless a focused filter is given.
- Pending-model check (run for all three contexts):
  ```
  dotnet ef migrations has-pending-model-changes --context SubmissionDbContext --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
  dotnet ef migrations has-pending-model-changes --context NotificationsDbContext --project src/Modules/Notifications/LIAnsureProtect.Modules.Notifications.Infrastructure/LIAnsureProtect.Modules.Notifications.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
  dotnet ef migrations has-pending-model-changes --context UnderwritingDbContext --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
  ```
- Module project short paths used below:
  - Domain: `src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Domain`
  - Application: `src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Application`
  - Infrastructure: `src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure`

---

# PHASE A — Build the new module home (legacy untouched; everything compiles & tests stay green)

## Task A1: Move the referral enums into the module Domain

**Files:**
- Create: `…Underwriting.Domain/Referrals/ReferralOperationStatus.cs`
- Create: `…Underwriting.Domain/Referrals/ReferralPriority.cs`
- Create: `…Underwriting.Domain/Referrals/ReferralTimelineEntryType.cs`
- (Legacy copies in `src/LIAnsureProtect.Domain/Quotes/` stay for now — deleted in Phase C.)

- [ ] **Step 1: Copy each enum into the module, changing only the namespace**

For each of the three legacy files (`ReferralOperationStatus.cs`, `ReferralPriority.cs`, `ReferralTimelineEntryType.cs`), copy the file verbatim and change the namespace line from `namespace LIAnsureProtect.Domain.Quotes;` to:

```csharp
namespace LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
```

Read each legacy enum first and reproduce its members exactly. `ReferralTimelineEntryType` must include every member the aggregate uses (`OperationCreated`, `AssignmentChanged`, `PriorityChanged`, `StatusChanged`, `DueDateChanged`, `NoteAdded`, `TaskAdded`, `TaskCompleted`, `DecisionRecorded`, `EvidenceRequestCreated`, `EvidenceRequestResponded`, `EvidenceRequestAccepted`, `EvidenceRequestReviewDecisionRecorded`, `EvidenceRequestCancelled`, `EvidenceRequestFollowUpSent`).

- [ ] **Step 2: Build**

Run: `dotnet build LIAnsureProtect.slnx --no-restore`
Expected: PASS, 0/0.

- [ ] **Step 3: Commit**

```bash
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Domain/Referrals
git commit -m "feat(underwriting): add referral enums to module domain"
```

## Task A2: Move the child entities into the module Domain

**Files:**
- Create: `…Underwriting.Domain/Referrals/QuoteReferralWorkNote.cs`
- Create: `…Underwriting.Domain/Referrals/QuoteReferralFollowUpTask.cs`
- Create: `…Underwriting.Domain/Referrals/QuoteReferralTimelineEntry.cs`

- [ ] **Step 1: Copy each child entity verbatim, changing only the namespace**

Copy the three legacy files from `src/LIAnsureProtect.Domain/Quotes/` to the module `Referrals/` folder. Change `namespace LIAnsureProtect.Domain.Quotes;` → `namespace LIAnsureProtect.Modules.Underwriting.Domain.Referrals;`. These entities reference no Quoting type (only `ReferralTimelineEntryType`, now in the same namespace), so no other edit is needed. Verify by reading each file that the only external type used is the moved enum.

- [ ] **Step 2: Build**

Run: `dotnet build LIAnsureProtect.slnx --no-restore`
Expected: PASS, 0/0.

- [ ] **Step 3: Commit**

```bash
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Domain/Referrals
git commit -m "feat(underwriting): add referral child entities to module domain"
```

## Task A3: Move the aggregate root, converting Quoting-enum params to strings

**Files:**
- Create: `…Underwriting.Domain/Referrals/QuoteReferralOperation.cs`

The legacy aggregate takes three Quoting-context types as parameters: `CyberRiskTier` (in `CreateDefault`), `QuoteUnderwritingDecision` (in `CloseForDecision`), `EvidenceReviewDecisionStatus` (in `RecordEvidenceRequestReviewDecision`). The module cannot reference those. Convert each to `string` at the boundary.

- [ ] **Step 1: Copy `QuoteReferralOperation.cs` verbatim with these exact edits**

1. Namespace → `namespace LIAnsureProtect.Modules.Underwriting.Domain.Referrals;`
2. `CreateDefault` signature: change `CyberRiskTier riskTier` → `string riskTier`, and replace the priority line:

```csharp
    public static QuoteReferralOperation CreateDefault(
        Guid quoteId,
        string riskTier,
        DateTime referredAtUtc,
        DateTime quoteExpiresAtUtc)
    {
        if (quoteId == Guid.Empty)
            throw new ArgumentException("Quote id is required.", nameof(quoteId));

        if (quoteExpiresAtUtc < referredAtUtc)
            throw new InvalidOperationException("Referral due date cannot be calculated after quote expiry.");

        var priority = riskTier is "High" or "Severe"
            ? ReferralPriority.High
            : ReferralPriority.Normal;
        // …rest of the method body unchanged…
```

3. `CloseForDecision` signature: change `QuoteUnderwritingDecision decision` → `string decision`. The body uses `decision` only in an interpolated string (`$"...final underwriting decision {decision} was recorded."`), so no further change.
4. `RecordEvidenceRequestReviewDecision` signature: change `EvidenceReviewDecisionStatus decision` → `string decision`. The body uses `decision` only in an interpolated string, so no further change.
5. Everything else (fields, all other methods, `RecordTimeline`, `EnsureOpen`, `ValidateRequiredUserId`) is copied verbatim.

- [ ] **Step 2: Build**

Run: `dotnet build LIAnsureProtect.slnx --no-restore`
Expected: PASS, 0/0.

- [ ] **Step 3: Move the aggregate unit tests to the module test surface**

- Create: `tests/LIAnsureProtect.UnitTests/Modules/Underwriting/Referrals/QuoteReferralOperationTests.cs`
- Copy `tests/LIAnsureProtect.UnitTests/Quotes/QuoteReferralOperationTests.cs` verbatim; change its `using LIAnsureProtect.Domain.Quotes;` to `using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;`; update any `CreateDefault(..., CyberRiskTier.High, ...)` call to pass `"High"` (string), and any `CloseForDecision(..., QuoteUnderwritingDecision.X, ...)` to pass `"X"`. Delete the legacy test file in this same step.

- [ ] **Step 4: Run the moved tests**

Run: `dotnet test LIAnsureProtect.slnx --no-build --filter "FullyQualifiedName~QuoteReferralOperationTests"`
Expected: PASS (same count as before the move).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Domain/Referrals/QuoteReferralOperation.cs tests/LIAnsureProtect.UnitTests/Modules/Underwriting/Referrals/QuoteReferralOperationTests.cs
git rm tests/LIAnsureProtect.UnitTests/Quotes/QuoteReferralOperationTests.cs
git commit -m "feat(underwriting): add referral operation aggregate to module domain"
```

## Task A4: Add the module read port + DTOs (queue summary + timeline)

**Files:**
- Create: `…Underwriting.Application/Referrals/IReferralOperationsReader.cs`

- [ ] **Step 1: Write the read port and result records**

```csharp
namespace LIAnsureProtect.Modules.Underwriting.Application.Referrals;

/// <summary>
/// Inbound read port the legacy referral-queue and timeline reads call to fetch the operation side of
/// the combined view. The module owns the operation data; the quote/evidence/decision-audit sides stay
/// legacy. Reads are no-tracking.
/// </summary>
public interface IReferralOperationsReader
{
    Task<IReadOnlyCollection<ReferralOperationSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> quoteIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ReferralOperationTimelineItem>?> GetTimelineAsync(
        Guid quoteId,
        CancellationToken cancellationToken);
}

public sealed record ReferralOperationSummary(
    Guid QuoteId,
    string? AssignedUnderwriterUserId,
    string Priority,
    DateTime DueAtUtc,
    bool IsSlaBreached,
    string Status,
    int OpenTaskCount,
    DateTime? LatestTimelineAtUtc);

public sealed record ReferralOperationTimelineItem(
    string EntryType,
    string Summary,
    string CreatedByUserId,
    DateTime CreatedAtUtc);
```

- [ ] **Step 2: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Application/Referrals/IReferralOperationsReader.cs
git commit -m "feat(underwriting): add referral operations read port"
```

## Task A5: Add the module write repository port

**Files:**
- Create: `…Underwriting.Application/Referrals/IReferralOperationRepository.cs`

- [ ] **Step 1: Write the repository port**

```csharp
using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;

namespace LIAnsureProtect.Modules.Underwriting.Application.Referrals;

/// <summary>
/// Module-owned persistence for the referral operation aggregate. Writes commit through the module's
/// own DbContext (no shared unit of work), mirroring the M35 AI-review repository.
/// </summary>
public interface IReferralOperationRepository
{
    Task AddAsync(QuoteReferralOperation operation, CancellationToken cancellationToken);

    /// <summary>Loads the tracked aggregate (with notes/tasks/timeline) for mutation, or null.</summary>
    Task<QuoteReferralOperation?> GetByQuoteIdForUpdateAsync(Guid quoteId, CancellationToken cancellationToken);

    /// <summary>True if an operation already exists for the quote (create-if-missing idempotency).</summary>
    Task<bool> ExistsForQuoteAsync(Guid quoteId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Application/Referrals/IReferralOperationRepository.cs
git commit -m "feat(underwriting): add referral operation repository port"
```

## Task A6: Extend the quote-context reader for referral create read-back

The create projector needs risk tier + referred-at + expiry, which `QuoteGeneratedDomainEvent` does not carry. Add a focused read-back method to the existing M35 reader port and its legacy adapter.

**Files:**
- Modify: `…Underwriting.Application/IUnderwritingQuoteContextReader.cs`
- Modify: `src/LIAnsureProtect.Infrastructure/Quotes/QuoteUnderwritingContextReader.cs`

- [ ] **Step 1: Add the method + record to the port**

Append to `IUnderwritingQuoteContextReader.cs`:

```csharp
public interface IUnderwritingQuoteContextReader
{
    Task<UnderwritingQuoteContext?> GetForAiReviewAsync(Guid quoteId, CancellationToken cancellationToken);

    /// <summary>Minimal quote facts needed to create a referral operation (risk tier, referred-at, expiry).</summary>
    Task<ReferralQuoteContext?> GetForReferralOperationAsync(Guid quoteId, CancellationToken cancellationToken);
}

/// <summary>Read-only quote facts for creating a referral operation. RiskTier is a string (cross-context).</summary>
public sealed record ReferralQuoteContext(
    Guid QuoteId,
    string RiskTier,
    DateTime ReferredAtUtc,
    DateTime ExpiresAtUtc);
```

(Keep the existing `GetForAiReviewAsync` and `UnderwritingQuoteContext` declarations in the file.)

- [ ] **Step 2: Implement the method in the legacy adapter**

Add to `QuoteUnderwritingContextReader.cs` (it already has `IQuoteRepository`):

```csharp
    public async Task<ReferralQuoteContext?> GetForReferralOperationAsync(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        var quote = await quoteRepository.GetForUnderwritingReviewAsync(quoteId, cancellationToken);
        if (quote is null)
            return null;

        return new ReferralQuoteContext(
            quote.Id,
            quote.RiskTier.ToString(),
            quote.CreatedAtUtc,
            quote.ExpiresAtUtc);
    }
```

- [ ] **Step 3: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Application/IUnderwritingQuoteContextReader.cs src/LIAnsureProtect.Infrastructure/Quotes/QuoteUnderwritingContextReader.cs
git commit -m "feat(underwriting): extend quote-context reader for referral create read-back"
```

## Task A7: Add the inbound projection port + event DTO

**Files:**
- Create: `…Underwriting.Application/Referrals/IReferralOperationProjector.cs`

- [ ] **Step 1: Write the projection port and its event DTO**

```csharp
namespace LIAnsureProtect.Modules.Underwriting.Application.Referrals;

/// <summary>
/// Inbound port the outbox dispatcher calls to project a Quoting-context domain event onto the referral
/// operation aggregate. Implementations MUST be idempotent on <see cref="ReferralOperationEvent.SourceOutboxMessageId"/>
/// (the dispatcher delivers at-least-once). Create is additionally create-if-missing so a referred quote's
/// operation appears with no user-visible gap.
/// </summary>
public interface IReferralOperationProjector
{
    Task ProjectAsync(ReferralOperationEvent referralEvent, CancellationToken cancellationToken);
}

public enum ReferralOperationEventKind
{
    Created,
    DecisionRecorded,
    EvidenceRequestCreated,
    EvidenceRequestResponded,
    EvidenceRequestAccepted,
    EvidenceRequestReviewDecisionRecorded,
    EvidenceRequestCancelled,
    EvidenceRequestFollowUpSent
}

/// <summary>
/// Context-neutral projection event mapped from an outbox message on the legacy side (so the module never
/// references the legacy outbox or Quoting events). All cross-context values are primitives.
/// </summary>
public sealed record ReferralOperationEvent(
    Guid SourceOutboxMessageId,
    ReferralOperationEventKind Kind,
    Guid QuoteId,
    string ActorUserId,
    DateTime OccurredAtUtc,
    Guid? EvidenceRequestId,
    string? Decision);
```

- [ ] **Step 2: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Application/Referrals/IReferralOperationProjector.cs
git commit -m "feat(underwriting): add referral operation projection port"
```

## Task A8: Move the write commands into the module Application

The six underwriter actions (assign, release, triage, add-note, add-task, complete-task) plus their result records and the two factory helpers move into the module. The `GetQuoteReferralTimelineQuery` does **not** move (it stays legacy in Phase B as a reader-port consumer).

**Files:**
- Create: `…Underwriting.Application/Referrals/Commands/ManageReferralOperations/ReferralOperationCommands.cs`

- [ ] **Step 1: Create the commands file**

Reproduce, from the legacy `QuoteReferralOperationCommands.cs`, **only** the six commands, their handlers, the result records (`QuoteReferralOperationResult`, `QuoteReferralNoteResult`, `QuoteReferralTaskResult`), and the two factory helpers (`QuoteReferralOperationResultFactory`, `QuoteReferralTaskResultFactory`) and `CurrentUnderwriterUser`. Apply these edits:

1. Namespace → `namespace LIAnsureProtect.Modules.Underwriting.Application.Referrals.Commands.ManageReferralOperations;`
2. Usings: replace `using LIAnsureProtect.Application.Common.Persistence;` and `using LIAnsureProtect.Domain.Quotes;` with
   ```csharp
   using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
   using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
   using LIAnsureProtect.Platform.Abstractions.Security;
   using MediatR;
   ```
3. Replace the constructor dependency `IQuoteRepository quoteRepository, IUnitOfWork unitOfWork, ICurrentUser currentUser` in each handler with `IReferralOperationRepository operations, ICurrentUser currentUser`.
4. In each handler body, replace `await quoteRepository.GetReferralOperationForUpdateAsync(request.QuoteId, ct)` with `await operations.GetByQuoteIdForUpdateAsync(request.QuoteId, ct)`, and replace `await unitOfWork.SaveChangesAsync(ct)` with `await operations.SaveChangesAsync(ct)`.
5. **Do not** copy `GetQuoteReferralTimelineQuery`/its handler/`QuoteReferralTimelineResult`/`QuoteReferralTimelineEntryResult` — those stay legacy (Phase B).

Worked example for one handler (apply the same shape to the other five):

```csharp
public sealed class AssignQuoteReferralToMeCommandHandler(
    IReferralOperationRepository operations,
    ICurrentUser currentUser)
    : IRequestHandler<AssignQuoteReferralToMeCommand, QuoteReferralOperationResult?>
{
    public async Task<QuoteReferralOperationResult?> Handle(
        AssignQuoteReferralToMeCommand request,
        CancellationToken cancellationToken)
    {
        var operation = await operations.GetByQuoteIdForUpdateAsync(request.QuoteId, cancellationToken);
        if (operation is null)
            return null;

        operation.AssignTo(CurrentUnderwriterUser.GetRequiredUserId(currentUser), DateTime.UtcNow);
        await operations.SaveChangesAsync(cancellationToken);

        return QuoteReferralOperationResultFactory.FromOperation(operation);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build LIAnsureProtect.slnx --no-restore`
Expected: PASS, 0/0. (Legacy command file still exists and still compiles — duplication is intentional until Phase C.)

- [ ] **Step 3: Commit**

```bash
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Application/Referrals/Commands
git commit -m "feat(underwriting): add referral operation write commands to module"
```

## Task A9: Add the projected-message dedupe entity + the EF configurations

**Files:**
- Create: `…Underwriting.Domain/Referrals/ReferralOperationProjectedMessage.cs`
- Create: `…Underwriting.Infrastructure/Persistence/QuoteReferralOperationConfiguration.cs`
- Create: `…Underwriting.Infrastructure/Persistence/QuoteReferralWorkNoteConfiguration.cs`
- Create: `…Underwriting.Infrastructure/Persistence/QuoteReferralFollowUpTaskConfiguration.cs`
- Create: `…Underwriting.Infrastructure/Persistence/QuoteReferralTimelineEntryConfiguration.cs`
- Create: `…Underwriting.Infrastructure/Persistence/ReferralOperationProjectedMessageConfiguration.cs`

- [ ] **Step 1: Add the dedupe entity**

```csharp
namespace LIAnsureProtect.Modules.Underwriting.Domain.Referrals;

/// <summary>
/// Idempotency marker: one row per source outbox-message id the projector has applied. Lets the
/// at-least-once dispatcher re-deliver safely (close/evidence projections append timeline entries and
/// are not naturally idempotent).
/// </summary>
public sealed class ReferralOperationProjectedMessage
{
    private ReferralOperationProjectedMessage(Guid sourceOutboxMessageId, DateTime appliedAtUtc)
    {
        SourceOutboxMessageId = sourceOutboxMessageId;
        AppliedAtUtc = appliedAtUtc;
    }

    private ReferralOperationProjectedMessage()
    {
    }

    public Guid SourceOutboxMessageId { get; private set; }

    public DateTime AppliedAtUtc { get; private set; }

    public static ReferralOperationProjectedMessage Record(Guid sourceOutboxMessageId, DateTime appliedAtUtc)
        => new(sourceOutboxMessageId, appliedAtUtc);
}
```

- [ ] **Step 2: Move the four EF configs, dropping the cross-context Quote FK**

Copy each legacy config from `src/LIAnsureProtect.Infrastructure/Persistence/Configurations/` into the module `Persistence/` folder. For all four: change `namespace …Infrastructure.Persistence.Configurations;` → `namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;` and `using LIAnsureProtect.Domain.Quotes;` → `using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;`. In `QuoteReferralOperationConfiguration`, **delete** the cross-context FK block (reference by id only):

```csharp
        // DELETE these lines — no cross-context navigation/FK in a modular monolith:
        builder.HasOne<Quote>()
            .WithMany()
            .HasForeignKey(operation => operation.QuoteId)
            .OnDelete(DeleteBehavior.Restrict);
```

Keep `ToTable(...)` (schema comes from `UnderwritingDbContext.HasDefaultSchema("underwriting")`), the unique `ux_quote_referral_operations_quote_id` index, the status/priority/due index, and the three `Navigation(...).UsePropertyAccessMode(PropertyAccessMode.Field)` lines. For the child configs, keep their existing within-aggregate `HasOne(...)`/`WithMany()` relationships to the operation (those are same-aggregate, not cross-context) and verify they reference no `Quote`/`Submission` type — if a child config has a `HasOne<Quote>`/`HasOne<Submission>` FK, delete it too.

- [ ] **Step 3: Add the dedupe config**

```csharp
using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class ReferralOperationProjectedMessageConfiguration
    : IEntityTypeConfiguration<ReferralOperationProjectedMessage>
{
    public void Configure(EntityTypeBuilder<ReferralOperationProjectedMessage> builder)
    {
        builder.ToTable("referral_operation_projected_messages");
        builder.HasKey(message => message.SourceOutboxMessageId);
        builder.Property(message => message.SourceOutboxMessageId)
            .HasColumnName("source_outbox_message_id")
            .ValueGeneratedNever();
        builder.Property(message => message.AppliedAtUtc)
            .HasColumnName("applied_at_utc")
            .IsRequired();
    }
}
```

- [ ] **Step 4: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Domain/Referrals/ReferralOperationProjectedMessage.cs src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/Persistence
git commit -m "feat(underwriting): add referral EF configs and dedupe marker"
```

## Task A10: Register the new DbSets on UnderwritingDbContext

**Files:**
- Modify: `…Underwriting.Infrastructure/Persistence/UnderwritingDbContext.cs`

- [ ] **Step 1: Add DbSets**

Add inside the class (the assembly config scan already picks up the new `IEntityTypeConfiguration`s):

```csharp
    public DbSet<QuoteReferralOperation> QuoteReferralOperations => Set<QuoteReferralOperation>();

    public DbSet<ReferralOperationProjectedMessage> ReferralOperationProjectedMessages
        => Set<ReferralOperationProjectedMessage>();
```

Add `using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;` at the top. (Work notes, tasks, and timeline entries are owned navigations of the operation aggregate and do not need their own root DbSet.)

- [ ] **Step 2: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/Persistence/UnderwritingDbContext.cs
git commit -m "feat(underwriting): map referral operation tables on UnderwritingDbContext"
```

## Task A11: Implement the module repository + reader + projector

**Files:**
- Create: `…Underwriting.Infrastructure/Persistence/EfReferralOperationRepository.cs`
- Create: `…Underwriting.Infrastructure/Persistence/ReferralOperationsReader.cs`
- Create: `…Underwriting.Infrastructure/Persistence/ReferralOperationProjector.cs`

- [ ] **Step 1: Repository**

```csharp
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class EfReferralOperationRepository(UnderwritingDbContext dbContext)
    : IReferralOperationRepository
{
    public async Task AddAsync(QuoteReferralOperation operation, CancellationToken cancellationToken)
        => await dbContext.QuoteReferralOperations.AddAsync(operation, cancellationToken);

    public Task<QuoteReferralOperation?> GetByQuoteIdForUpdateAsync(Guid quoteId, CancellationToken cancellationToken)
        => dbContext.QuoteReferralOperations
            .Include(operation => operation.Notes)
            .Include(operation => operation.Tasks)
            .Include(operation => operation.TimelineEntries)
            .SingleOrDefaultAsync(operation => operation.QuoteId == quoteId, cancellationToken);

    public Task<bool> ExistsForQuoteAsync(Guid quoteId, CancellationToken cancellationToken)
        => dbContext.QuoteReferralOperations.AnyAsync(operation => operation.QuoteId == quoteId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
```

- [ ] **Step 2: Reader (no-tracking)**

```csharp
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class ReferralOperationsReader(UnderwritingDbContext dbContext) : IReferralOperationsReader
{
    public async Task<IReadOnlyCollection<ReferralOperationSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> quoteIds,
        CancellationToken cancellationToken)
    {
        if (quoteIds.Count == 0)
            return [];

        var operations = await dbContext.QuoteReferralOperations
            .AsNoTracking()
            .Include(operation => operation.Tasks)
            .Include(operation => operation.TimelineEntries)
            .Where(operation => quoteIds.Contains(operation.QuoteId))
            .ToListAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        return operations.Select(operation => ToSummary(operation, nowUtc)).ToList();
    }

    public async Task<IReadOnlyCollection<ReferralOperationTimelineItem>?> GetTimelineAsync(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        var operation = await dbContext.QuoteReferralOperations
            .AsNoTracking()
            .Include(candidate => candidate.TimelineEntries)
            .SingleOrDefaultAsync(candidate => candidate.QuoteId == quoteId, cancellationToken);

        if (operation is null)
            return null;

        return operation.TimelineEntries
            .Select(entry => new ReferralOperationTimelineItem(
                entry.EntryType.ToString(),
                entry.Summary,
                entry.CreatedByUserId,
                entry.CreatedAtUtc))
            .ToList();
    }

    private static ReferralOperationSummary ToSummary(QuoteReferralOperation operation, DateTime nowUtc)
        => new(
            operation.QuoteId,
            operation.AssignedUnderwriterUserId,
            operation.Priority.ToString(),
            operation.DueAtUtc,
            operation.DueAtUtc < nowUtc && operation.Status != ReferralOperationStatus.Closed,
            operation.Status.ToString(),
            operation.Tasks.Count(task => !task.IsCompleted),
            operation.TimelineEntries
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Select(entry => (DateTime?)entry.CreatedAtUtc)
                .FirstOrDefault());
}
```

- [ ] **Step 3: Projector (idempotent, create-if-missing, self-healing)**

```csharp
using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

/// <summary>
/// Projects Quoting-context events onto the referral operation aggregate. Idempotent on the source
/// outbox-message id; create is additionally create-if-missing so a referred quote's operation appears
/// with no user-visible gap, and close/evidence self-heal by ensuring the operation exists first.
/// </summary>
public sealed class ReferralOperationProjector(
    UnderwritingDbContext dbContext,
    IUnderwritingQuoteContextReader quoteContextReader) : IReferralOperationProjector
{
    private const string SystemUserId = "system";

    public async Task ProjectAsync(ReferralOperationEvent referralEvent, CancellationToken cancellationToken)
    {
        var alreadyApplied = await dbContext.ReferralOperationProjectedMessages
            .AnyAsync(message => message.SourceOutboxMessageId == referralEvent.SourceOutboxMessageId, cancellationToken);
        if (alreadyApplied)
            return;

        var operation = await EnsureOperationAsync(referralEvent.QuoteId, cancellationToken);
        if (operation is null)
            return; // quote facts not yet readable; the dispatcher retries this message later.

        Apply(referralEvent, operation);

        dbContext.ReferralOperationProjectedMessages.Add(
            ReferralOperationProjectedMessage.Record(referralEvent.SourceOutboxMessageId, DateTime.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<QuoteReferralOperation?> EnsureOperationAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        var operation = await dbContext.QuoteReferralOperations
            .Include(candidate => candidate.Notes)
            .Include(candidate => candidate.Tasks)
            .Include(candidate => candidate.TimelineEntries)
            .SingleOrDefaultAsync(candidate => candidate.QuoteId == quoteId, cancellationToken);
        if (operation is not null)
            return operation;

        var quote = await quoteContextReader.GetForReferralOperationAsync(quoteId, cancellationToken);
        if (quote is null)
            return null;

        operation = QuoteReferralOperation.CreateDefault(
            quote.QuoteId, quote.RiskTier, quote.ReferredAtUtc, quote.ExpiresAtUtc);
        await dbContext.QuoteReferralOperations.AddAsync(operation, cancellationToken);
        return operation;
    }

    private static void Apply(ReferralOperationEvent referralEvent, QuoteReferralOperation operation)
    {
        var actor = string.IsNullOrWhiteSpace(referralEvent.ActorUserId) ? SystemUserId : referralEvent.ActorUserId;
        var at = referralEvent.OccurredAtUtc;
        var evidenceId = referralEvent.EvidenceRequestId ?? Guid.Empty;

        switch (referralEvent.Kind)
        {
            case ReferralOperationEventKind.Created:
                break; // EnsureOperationAsync already created it.
            case ReferralOperationEventKind.DecisionRecorded:
                operation.CloseForDecision(actor, referralEvent.Decision ?? string.Empty, at);
                break;
            case ReferralOperationEventKind.EvidenceRequestCreated:
                operation.RecordEvidenceRequestCreated(evidenceId, actor, at);
                break;
            case ReferralOperationEventKind.EvidenceRequestResponded:
                operation.RecordEvidenceRequestResponded(evidenceId, actor, at);
                break;
            case ReferralOperationEventKind.EvidenceRequestAccepted:
                operation.RecordEvidenceRequestAccepted(evidenceId, actor, at);
                break;
            case ReferralOperationEventKind.EvidenceRequestReviewDecisionRecorded:
                operation.RecordEvidenceRequestReviewDecision(evidenceId, referralEvent.Decision ?? string.Empty, actor, at);
                break;
            case ReferralOperationEventKind.EvidenceRequestCancelled:
                operation.RecordEvidenceRequestCancelled(evidenceId, actor, at);
                break;
            case ReferralOperationEventKind.EvidenceRequestFollowUpSent:
                operation.RecordEvidenceRequestFollowUpSent(evidenceId, actor, at);
                break;
        }
    }
}
```

> Note: `EnsureOperationAsync`/`Apply` are tolerant — if the aggregate is already closed and a late evidence event arrives, `EnsureOpen()` throws; the projector should treat a closed-operation mutation as a no-op. Add a guard in `Apply` only if a reworked integration test in Phase B reveals that ordering; keep it minimal.

- [ ] **Step 4: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/Persistence/EfReferralOperationRepository.cs src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/Persistence/ReferralOperationsReader.cs src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/Persistence/ReferralOperationProjector.cs
git commit -m "feat(underwriting): implement referral repository, reader, and projector"
```

## Task A12: Register the new services in AddUnderwritingModule

**Files:**
- Modify: `…Underwriting.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Register repository, reader, projector**

Add after the `IAiUnderwritingReviewRepository` registration:

```csharp
        services.AddScoped<IReferralOperationRepository, EfReferralOperationRepository>();
        services.AddScoped<IReferralOperationsReader, ReferralOperationsReader>();
        services.AddScoped<IReferralOperationProjector, ReferralOperationProjector>();
```

Add `using LIAnsureProtect.Modules.Underwriting.Application.Referrals;`. (MediatR already scans this assembly, so the moved command handlers register automatically.)

- [ ] **Step 2: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/DependencyInjection.cs
git commit -m "feat(underwriting): register referral operation services"
```

## Task A13: Add the CreateReferralOperations migration (underwriting schema)

**Files:**
- Create: migration under `…Underwriting.Infrastructure/Migrations/` (generated)

- [ ] **Step 1: Generate the migration**

Run:
```
dotnet ef migrations add CreateReferralOperations --context UnderwritingDbContext --project src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/LIAnsureProtect.Modules.Underwriting.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
```
Expected: a new migration creating `quote_referral_operations`, `quote_referral_work_notes`, `quote_referral_follow_up_tasks`, `quote_referral_timeline_entries`, and `referral_operation_projected_messages` in the `underwriting` schema. Open it and confirm there is **no** FK to a `quotes`/`submissions` table.

- [ ] **Step 2: Verify no pending model changes for UnderwritingDbContext**

Run the `has-pending-model-changes --context UnderwritingDbContext …` command from the conventions block.
Expected: "No changes have been made…".

- [ ] **Step 3: Add a migration test**

- Create: `tests/LIAnsureProtect.IntegrationTests/Underwriting/CreateReferralOperationsMigrationTests.cs`
- Mirror the existing `CreateUnderwritingSchema` migration test (find it under the Underwriting integration tests). Assert the four referral tables + the dedupe table exist in the `underwriting` schema after migrating. Reuse the existing test's PostgreSQL fixture/pattern verbatim.

- [ ] **Step 4: Run the migration test**

Run: `dotnet test LIAnsureProtect.slnx --filter "FullyQualifiedName~CreateReferralOperationsMigrationTests"`
Expected: PASS (or SKIPPED if it is gated behind the PostgreSQL opt-in flag, matching the existing migration test's gating).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/Migrations tests/LIAnsureProtect.IntegrationTests/Underwriting/CreateReferralOperationsMigrationTests.cs
git commit -m "feat(underwriting): add CreateReferralOperations migration"
```

---

# PHASE B — Cut every caller over to the module (events + ports), keep tests green

## Task B1: Unit-test the projector idempotency & create-if-missing

**Files:**
- Create: `tests/LIAnsureProtect.UnitTests/Modules/Underwriting/Referrals/ReferralOperationProjectorTests.cs`

- [ ] **Step 1: Write failing tests**

Use an in-memory `UnderwritingDbContext` (the existing module tests show the pattern — reuse it) and a Moq `IUnderwritingQuoteContextReader`.

```csharp
[Fact]
public async Task Created_event_creates_operation_once_and_is_idempotent()
{
    // reader returns ReferralQuoteContext(quoteId, "High", referredAt, expiresAt)
    // ProjectAsync(Created) twice with the SAME SourceOutboxMessageId → exactly one operation, one marker.
}

[Fact]
public async Task Decision_event_self_heals_when_operation_missing_then_closes()
{
    // No operation yet; reader returns referral context.
    // ProjectAsync(DecisionRecorded, decision:"Approved") → operation exists AND Status == Closed.
}

[Fact]
public async Task Duplicate_source_message_id_is_a_no_op()
{
    // Apply DecisionRecorded twice with same id → only one Closed timeline entry (no double append).
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test LIAnsureProtect.slnx --filter "FullyQualifiedName~ReferralOperationProjectorTests"`
Expected: FAIL (assertions on count/status).

- [ ] **Step 3: Make them pass**

The projector from Task A11 should satisfy these. Fix the projector if a test reveals a gap (e.g. add the closed-operation no-op guard noted in A11).

- [ ] **Step 4: Run to verify pass; commit**

```bash
dotnet test LIAnsureProtect.slnx --filter "FullyQualifiedName~ReferralOperationProjectorTests"
git add tests/LIAnsureProtect.UnitTests/Modules/Underwriting/Referrals/ReferralOperationProjectorTests.cs src/Modules/Underwriting/LIAnsureProtect.Modules.Underwriting.Infrastructure/Persistence/ReferralOperationProjector.cs
git commit -m "test(underwriting): cover referral projector idempotency and self-heal"
```

## Task B2: Add the legacy outbox→referral event mapper

**Files:**
- Create: `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxReferralOperationMapper.cs`

- [ ] **Step 1: Write the mapper**

```csharp
using System.Text.Json;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

/// <summary>
/// Legacy-side mapper from an outbox message (a serialized Quoting domain event) to the module's
/// context-neutral <see cref="ReferralOperationEvent"/>. Returns null for events the referral operation
/// does not react to.
/// </summary>
internal static class OutboxReferralOperationMapper
{
    public static ReferralOperationEvent? TryMap(OutboxMessage outboxMessage)
    {
        return outboxMessage.Type switch
        {
            nameof(QuoteGeneratedDomainEvent) => MapGenerated(outboxMessage),
            nameof(QuoteUnderwritingDecisionRecordedDomainEvent) => MapDecision(outboxMessage),
            nameof(QuoteEvidenceRequestCreatedDomainEvent) => MapEvidence(outboxMessage, ReferralOperationEventKind.EvidenceRequestCreated),
            nameof(QuoteEvidenceRequestRespondedDomainEvent) => MapEvidence(outboxMessage, ReferralOperationEventKind.EvidenceRequestResponded),
            nameof(QuoteEvidenceRequestAcceptedDomainEvent) => MapEvidenceAccepted(outboxMessage),
            nameof(QuoteEvidenceRequestCancelledDomainEvent) => MapEvidence(outboxMessage, ReferralOperationEventKind.EvidenceRequestCancelled),
            nameof(QuoteEvidenceRequestFollowUpSentDomainEvent) => MapEvidence(outboxMessage, ReferralOperationEventKind.EvidenceRequestFollowUpSent),
            nameof(QuoteEvidenceRequestRemediationRequiredDomainEvent) => MapRemediation(outboxMessage),
            _ => null
        };
    }

    private static ReferralOperationEvent? MapGenerated(OutboxMessage outboxMessage)
    {
        var domainEvent = Deserialize<QuoteGeneratedDomainEvent>(outboxMessage);
        if (domainEvent.Status != QuoteStatus.Referred)
            return null;

        return new ReferralOperationEvent(
            outboxMessage.Id, ReferralOperationEventKind.Created, domainEvent.QuoteId,
            "system", domainEvent.OccurredAtUtc, null, null);
    }

    private static ReferralOperationEvent MapDecision(OutboxMessage outboxMessage)
    {
        var domainEvent = Deserialize<QuoteUnderwritingDecisionRecordedDomainEvent>(outboxMessage);
        return new ReferralOperationEvent(
            outboxMessage.Id, ReferralOperationEventKind.DecisionRecorded, domainEvent.QuoteId,
            domainEvent.ReviewedByUserId, domainEvent.OccurredAtUtc, null, domainEvent.Decision.ToString());
    }

    private static ReferralOperationEvent MapEvidence(OutboxMessage outboxMessage, ReferralOperationEventKind kind)
    {
        // QuoteEvidenceRequestCreated/Responded/Cancelled/FollowUpSent share the same shape we need:
        // EvidenceRequestId, QuoteId, an actor user id, OccurredAtUtc.
        using var document = JsonDocument.Parse(outboxMessage.Payload);
        var root = document.RootElement;
        var quoteId = root.GetProperty("QuoteId").GetGuid();
        var evidenceRequestId = root.GetProperty("EvidenceRequestId").GetGuid();
        var actor = ActorFor(kind, root);
        var occurredAtUtc = root.GetProperty("OccurredAtUtc").GetDateTime();

        return new ReferralOperationEvent(
            outboxMessage.Id, kind, quoteId, actor, occurredAtUtc, evidenceRequestId, null);
    }

    private static ReferralOperationEvent MapEvidenceAccepted(OutboxMessage outboxMessage)
    {
        var domainEvent = Deserialize<QuoteEvidenceRequestAcceptedDomainEvent>(outboxMessage);
        return new ReferralOperationEvent(
            outboxMessage.Id, ReferralOperationEventKind.EvidenceRequestAccepted, domainEvent.QuoteId,
            domainEvent.AcceptedByUserId, domainEvent.OccurredAtUtc, domainEvent.EvidenceRequestId, null);
    }

    private static ReferralOperationEvent MapRemediation(OutboxMessage outboxMessage)
    {
        var domainEvent = Deserialize<QuoteEvidenceRequestRemediationRequiredDomainEvent>(outboxMessage);
        return new ReferralOperationEvent(
            outboxMessage.Id, ReferralOperationEventKind.EvidenceRequestReviewDecisionRecorded, domainEvent.QuoteId,
            domainEvent.ReviewedByUserId, domainEvent.OccurredAtUtc, domainEvent.EvidenceRequestId,
            domainEvent.Decision.ToString());
    }

    private static string ActorFor(ReferralOperationEventKind kind, JsonElement root) => kind switch
    {
        ReferralOperationEventKind.EvidenceRequestCreated => root.GetProperty("RequestedByUserId").GetString() ?? "system",
        ReferralOperationEventKind.EvidenceRequestResponded => root.GetProperty("RespondedByUserId").GetString() ?? "system",
        ReferralOperationEventKind.EvidenceRequestCancelled => root.GetProperty("CancelledByUserId").GetString() ?? "system",
        ReferralOperationEventKind.EvidenceRequestFollowUpSent => root.GetProperty("FollowedUpByUserId").GetString() ?? "system",
        _ => "system"
    };

    private static T Deserialize<T>(OutboxMessage outboxMessage)
        => JsonSerializer.Deserialize<T>(outboxMessage.Payload)
            ?? throw new InvalidOperationException($"Outbox message {outboxMessage.Id} payload could not be deserialized.");
}
```

> The current accept path records both `EvidenceRequestAccepted` and `EvidenceRequestReviewDecision` timeline entries (see the mapping caveat in the design spec). The accepted event maps to `EvidenceRequestAccepted`; the review-decision entry is produced by the `RemediationRequired`/`Accepted` review path. If a reworked integration test in Task B7 shows a missing entry for the satisfied-accept case, emit a second `ReferralOperationEvent` for it (the mapper returns a single event today; widen to a small list there only if the test requires it).

- [ ] **Step 2: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxReferralOperationMapper.cs
git commit -m "feat(outbox): map outbox messages to referral operation events"
```

## Task B3: Fan the dispatcher out to the referral projector

**Files:**
- Modify: `src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcher.cs`

- [ ] **Step 1: Inject and invoke the projector before notification mapping**

1. Add constructor parameter `IReferralOperationProjector referralOperationProjector` (and `using LIAnsureProtect.Modules.Underwriting.Application.Referrals;`).
2. Inside the `foreach (var message in pendingMessages)` loop, **before** `var notificationMessage = OutboxNotificationMapper.TryMap(message);`, add:

```csharp
            var referralEvent = OutboxReferralOperationMapper.TryMap(message);
            if (referralEvent is not null)
                await referralOperationProjector.ProjectAsync(referralEvent, cancellationToken);
```

This runs the referral projection idempotently alongside notification projection. Messages that are notification-only or referral-only each take the path they need; messages that are both get both. Ordering matches M33 (project before publish/mark).

- [ ] **Step 2: Build**

Run: `dotnet build LIAnsureProtect.slnx --no-restore`
Expected: PASS, 0/0.

- [ ] **Step 3: Update the dispatcher integration test wiring**

- Modify: `tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs`
- The dispatcher is now constructed with an extra dependency. If the test news up `OutboxDispatcher` directly, pass a real `ReferralOperationProjector` (over an in-memory `UnderwritingDbContext` + a stub `IUnderwritingQuoteContextReader`) or resolve the dispatcher from the host. Keep existing assertions intact.

- [ ] **Step 4: Run dispatcher tests; commit**

```bash
dotnet test LIAnsureProtect.slnx --filter "FullyQualifiedName~OutboxDispatcher"
git add src/LIAnsureProtect.Infrastructure/Persistence/Outbox/OutboxDispatcher.cs tests/LIAnsureProtect.IntegrationTests/OutboxDispatcherTests.cs
git commit -m "feat(outbox): project referral operation events from the dispatcher"
```

## Task B4: Stop the legacy create path writing the operation

**Files:**
- Modify: `src/LIAnsureProtect.Application/Quotes/Commands/CreateQuote/CreateQuoteCommandHandler.cs`

- [ ] **Step 1: Remove the inline operation creation**

Delete this block (the operation is now created by the `QuoteGeneratedDomainEvent` projector):

```csharp
        if (quote.Status == QuoteStatus.Referred)
        {
            var operation = QuoteReferralOperation.CreateDefault(
                quote.Id,
                quote.RiskTier,
                quote.CreatedAtUtc,
                quote.ExpiresAtUtc);
            await quoteRepository.AddReferralOperationAsync(operation, cancellationToken);
        }
```

Leave the rest of the handler (quote add, provider attempt, save) unchanged. `Quote.Generate` already raises `QuoteGeneratedDomainEvent` captured to the outbox.

- [ ] **Step 2: Build**

Run: `dotnet build LIAnsureProtect.slnx --no-restore`
Expected: PASS, 0/0.

- [ ] **Step 3: Commit**

```bash
git add src/LIAnsureProtect.Application/Quotes/Commands/CreateQuote/CreateQuoteCommandHandler.cs
git commit -m "refactor(quotes): create referral operation via event projection"
```

## Task B5: Stop the legacy decision handlers writing the operation

**Files:**
- Modify: `src/LIAnsureProtect.Application/Quotes/Commands/UnderwriteQuoteReferral/ApproveQuoteReferralCommandHandler.cs`
- Modify: `…/DeclineQuoteReferralCommandHandler.cs`
- Modify: `…/AdjustQuoteReferralCommandHandler.cs`

- [ ] **Step 1: Remove the operation load + CloseForDecision from each handler**

In each handler delete the two lines:

```csharp
        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(request.QuoteId, cancellationToken);
        operation?.CloseForDecision(reviewedByUserId, review.Decision, reviewedAtUtc);
```

The close is now driven by `QuoteUnderwritingDecisionRecordedDomainEvent` (already raised by `Quote.ApproveReferral/DeclineReferral/AdjustReferral` and captured to the outbox). Leave `AddUnderwritingReviewAsync` + `SaveChangesAsync` intact.

- [ ] **Step 2: Build & commit**

```bash
dotnet build LIAnsureProtect.slnx --no-restore
git add src/LIAnsureProtect.Application/Quotes/Commands/UnderwriteQuoteReferral
git commit -m "refactor(quotes): close referral operation via decision event"
```

## Task B6: Stop the legacy evidence handlers writing the operation

**Files:**
- Modify: `src/LIAnsureProtect.Application/Quotes/Commands/ManageQuoteEvidenceRequests/QuoteEvidenceRequestCommands.cs`

- [ ] **Step 1: Remove the 7 operation calls**

Remove every `var operation = await quoteRepository.GetReferralOperationForUpdateAsync(...)` and each subsequent `operation.RecordEvidenceRequest*(...)` call (the lines identified at the operation-call sites). These projections are now driven by the evidence domain events (already raised + captured). Keep all evidence-request state changes, the `AddEvidenceRequestAsync`/review/save calls, and the domain-event raising intact.

- [ ] **Step 2: Build**

Run: `dotnet build LIAnsureProtect.slnx --no-restore`
Expected: PASS, 0/0.

- [ ] **Step 3: Commit**

```bash
git add src/LIAnsureProtect.Application/Quotes/Commands/ManageQuoteEvidenceRequests/QuoteEvidenceRequestCommands.cs
git commit -m "refactor(quotes): project evidence activity to operation via events"
```

## Task B7: Point the two legacy reads at the module reader port

**Files:**
- Modify: `src/LIAnsureProtect.Application/LIAnsureProtect.Application.csproj`
- Modify: `src/LIAnsureProtect.Application/Quotes/Queries/ListQuoteReferrals/ListQuoteReferralsQueryHandler.cs`
- Modify: `src/LIAnsureProtect.Application/Quotes/Queries/ListQuoteReferrals/ListQuoteReferralsResult.cs` (only if the summary type needs no change — verify)
- Modify: the legacy `GetQuoteReferralTimelineQuery` + handler (in `…/ManageQuoteReferralOperations/QuoteReferralOperationCommands.cs`)

- [ ] **Step 1: Allow legacy Application → module Underwriting Application**

Add to `LIAnsureProtect.Application.csproj`:

```xml
    <ProjectReference Include="..\Modules\Underwriting\LIAnsureProtect.Modules.Underwriting.Application\LIAnsureProtect.Modules.Underwriting.Application.csproj" />
```

- [ ] **Step 2: Queue read uses the reader port**

In `ListQuoteReferralsQueryHandler`, replace the constructor with `(IQuoteRepository quoteRepository, IReferralOperationsReader referralOperationsReader)` and replace the operations fetch:

```csharp
        var operationSummaries = await referralOperationsReader.GetSummariesAsync(
            quotes.Select(quote => quote.Id).ToList(),
            cancellationToken);
        var operationsByQuoteId = operationSummaries.ToDictionary(summary => summary.QuoteId);
```

Then in the per-quote projection, map the `ReferralOperationSummary` into the existing `QuoteReferralOperationsSummaryResult` (same field order; `IsSlaBreached` already computed by the reader). Remove the old `quoteRepository.ListReferralOperationsAsync(...)` call and the `CreateOperationsSummary(QuoteReferralOperation)` helper. The evidence summary path is unchanged.

- [ ] **Step 3: Timeline read uses the reader port**

The legacy `GetQuoteReferralTimelineQueryHandler` keeps producing `QuoteReferralTimelineResult` but now sources operation entries from the reader + keeps the legacy decision-audit concat:

```csharp
public sealed class GetQuoteReferralTimelineQueryHandler(
    IQuoteRepository quoteRepository,
    IReferralOperationsReader referralOperationsReader)
    : IRequestHandler<GetQuoteReferralTimelineQuery, QuoteReferralTimelineResult?>
{
    public async Task<QuoteReferralTimelineResult?> Handle(
        GetQuoteReferralTimelineQuery request,
        CancellationToken cancellationToken)
    {
        var operationEntries = await referralOperationsReader.GetTimelineAsync(request.QuoteId, cancellationToken);
        if (operationEntries is null)
            return null;

        var reviews = await quoteRepository.ListUnderwritingReviewsAsync(request.QuoteId, cancellationToken);
        var entries = operationEntries
            .Select(entry => new QuoteReferralTimelineEntryResult(
                entry.EntryType, entry.Summary, entry.CreatedByUserId, entry.CreatedAtUtc))
            .Concat(reviews.Select(review => new QuoteReferralTimelineEntryResult(
                "DecisionRecorded",
                $"Final underwriting decision audit row recorded: {review.Decision}.",
                review.ReviewedByUserId,
                review.CreatedAtUtc)))
            .OrderBy(entry => entry.CreatedAtUtc)
            .ToList();

        return new QuoteReferralTimelineResult(request.QuoteId, entries);
    }
}
```

Keep `GetQuoteReferralTimelineQuery`, `QuoteReferralTimelineResult`, and `QuoteReferralTimelineEntryResult` in the legacy file; remove the six **write** commands/handlers/results/factories from it (they now live in the module — Task A8). Add `using LIAnsureProtect.Modules.Underwriting.Application.Referrals;`.

- [ ] **Step 4: Update the boundary ratchet**

In `tests/LIAnsureProtect.UnitTests/Architecture/ProjectReferenceBoundaryTests.cs`, change the Application row to include the new reference:

```csharp
    [InlineData(
        "src/LIAnsureProtect.Application/LIAnsureProtect.Application.csproj",
        "LIAnsureProtect.Domain",
        "LIAnsureProtect.Modules.Underwriting.Application")]
```

- [ ] **Step 5: Build & run architecture + read tests**

Run: `dotnet build LIAnsureProtect.slnx --no-restore`
Run: `dotnet test LIAnsureProtect.slnx --no-build --filter "FullyQualifiedName~ProjectReferenceBoundaryTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/LIAnsureProtect.Application/LIAnsureProtect.Application.csproj src/LIAnsureProtect.Application/Quotes/Queries/ListQuoteReferrals src/LIAnsureProtect.Application/Quotes/Commands/ManageQuoteReferralOperations tests/LIAnsureProtect.UnitTests/Architecture/ProjectReferenceBoundaryTests.cs
git commit -m "refactor(quotes): read referral operations via module reader port"
```

## Task B8: Point the controller at the module write commands

**Files:**
- Modify: `src/LIAnsureProtect.Api/Controllers/UnderwritingQuoteReferralsController.cs`

- [ ] **Step 1: Swap the command namespace + enum source**

1. Replace `using LIAnsureProtect.Application.Quotes.Commands.ManageQuoteReferralOperations;` with `using LIAnsureProtect.Modules.Underwriting.Application.Referrals.Commands.ManageReferralOperations;`.
2. The `Triage` action parses `ReferralPriority`/`ReferralOperationStatus`. Replace `using LIAnsureProtect.Domain.Quotes;` usage for those enums with `using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;`. (Other Quoting enums the controller still uses — `EvidenceRequestCategory`, `EvidenceReviewDecisionStatus` — stay on `LIAnsureProtect.Domain.Quotes`; keep both usings.)
3. `GetTimeline` still dispatches the legacy `GetQuoteReferralTimelineQuery` (unchanged namespace). `AddNote`/`AddTask`/etc. now dispatch the module commands (same record names, new namespace) — no body change beyond the using swap.

- [ ] **Step 2: Build**

Run: `dotnet build LIAnsureProtect.slnx --no-restore`
Expected: PASS, 0/0.

- [ ] **Step 3: Commit**

```bash
git add src/LIAnsureProtect.Api/Controllers/UnderwritingQuoteReferralsController.cs
git commit -m "refactor(api): dispatch referral operation commands to the module"
```

## Task B9: Rework the referral/evidence integration tests for eventual consistency

**Files:**
- Modify: `tests/LIAnsureProtect.IntegrationTests/UnderwritingReferralEndpointTests.cs`
- Modify: `tests/LIAnsureProtect.IntegrationTests/EvidenceDocumentEndpointTests.cs` (and any other test asserting operation/timeline/queue state after create/decision/evidence)

- [ ] **Step 1: Add a dispatcher-pump helper and use it**

Where a test creates a referred quote and then asserts operation/queue/timeline state, resolve `IOutboxDispatcher` from the factory and pump it before asserting (mirror `OutboxDispatcherTests`):

```csharp
    private static async Task PumpOutboxAsync(WebApplicationFactory<Program> factory, CancellationToken ct = default)
    {
        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
        // Drain until no more pending messages are processed (create → decision/evidence may chain).
        while (await dispatcher.DispatchPendingMessagesAsync(ct) > 0) { }
    }
```

For underwriter-action tests (assign/triage/note/task) that previously relied on the operation existing immediately after the quote was referred: either pump first, or rely on the projector having run — but the self-healing create-if-missing means an action against a not-yet-projected operation must still find/create it. Since the **write commands** call `GetByQuoteIdForUpdateAsync` (which does **not** self-heal — only the projector does), these tests MUST pump the outbox before the first operation action. Add `await PumpOutboxAsync(Factory);` after creating the referred quote and before the first operations call.

- [ ] **Step 2: Adjust assertions that checked synchronous side effects**

Any assertion of the form "immediately after approve, the operation is Closed" gets a `await PumpOutboxAsync(Factory);` between the approve call and the assertion. Timeline/queue assertions similarly pump first.

- [ ] **Step 3: Run the reworked tests**

Run: `dotnet test LIAnsureProtect.slnx --filter "FullyQualifiedName~UnderwritingReferralEndpointTests|FullyQualifiedName~EvidenceDocumentEndpointTests"`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add tests/LIAnsureProtect.IntegrationTests/UnderwritingReferralEndpointTests.cs tests/LIAnsureProtect.IntegrationTests/EvidenceDocumentEndpointTests.cs
git commit -m "test: pump outbox for eventually-consistent referral operations"
```

> Decision point surfaced here: if pumping in many tests proves noisy, reconsider whether underwriter write-commands should also self-heal (call `EnsureCreated`) — but that requires the reader port inside the module command path. Keep the current projector-only self-heal unless the test rework shows real friction; raise it in review.

---

# PHASE C — Delete the legacy aggregate & finish

## Task C1: Remove the referral methods from the legacy quote repository

**Files:**
- Modify: `src/LIAnsureProtect.Application/Quotes/IQuoteRepository.cs`
- Modify: `src/LIAnsureProtect.Infrastructure/Quotes/EfCoreQuoteRepository.cs`

- [ ] **Step 1: Delete the five referral-operation methods**

From `IQuoteRepository` remove: `AddReferralOperationAsync`, `ListReferralOperationsAsync`, `GetReferralOperationForUpdateAsync` (and confirm no other referral-op methods remain). From `EfCoreQuoteRepository` remove their implementations and any `DbSet`/`Include` referencing the referral aggregate.

- [ ] **Step 2: Build**

Run: `dotnet build LIAnsureProtect.slnx --no-restore`
Expected: PASS, 0/0 (all callers were removed in Phase B).

- [ ] **Step 3: Commit**

```bash
git add src/LIAnsureProtect.Application/Quotes/IQuoteRepository.cs src/LIAnsureProtect.Infrastructure/Quotes/EfCoreQuoteRepository.cs
git commit -m "refactor(quotes): drop legacy referral operation repository methods"
```

## Task C2: Drop the legacy aggregate, configs, DbSets, and the evidence FK

**Files:**
- Delete: `src/LIAnsureProtect.Domain/Quotes/QuoteReferralOperation.cs`, `QuoteReferralWorkNote.cs`, `QuoteReferralFollowUpTask.cs`, `QuoteReferralTimelineEntry.cs`, `ReferralOperationStatus.cs`, `ReferralPriority.cs`, `ReferralTimelineEntryType.cs`
- Delete: the four legacy `Configurations/QuoteReferral*Configuration.cs`
- Modify: `src/LIAnsureProtect.Infrastructure/Persistence/SubmissionDbContext.cs` (remove referral DbSets if any)
- Modify: `src/LIAnsureProtect.Infrastructure/Persistence/Configurations/QuoteEvidenceRequestConfiguration.cs` (drop the operation FK)

- [ ] **Step 1: Delete legacy referral files**

```bash
git rm src/LIAnsureProtect.Domain/Quotes/QuoteReferralOperation.cs src/LIAnsureProtect.Domain/Quotes/QuoteReferralWorkNote.cs src/LIAnsureProtect.Domain/Quotes/QuoteReferralFollowUpTask.cs src/LIAnsureProtect.Domain/Quotes/QuoteReferralTimelineEntry.cs src/LIAnsureProtect.Domain/Quotes/ReferralOperationStatus.cs src/LIAnsureProtect.Domain/Quotes/ReferralPriority.cs src/LIAnsureProtect.Domain/Quotes/ReferralTimelineEntryType.cs
git rm src/LIAnsureProtect.Infrastructure/Persistence/Configurations/QuoteReferralOperationConfiguration.cs src/LIAnsureProtect.Infrastructure/Persistence/Configurations/QuoteReferralWorkNoteConfiguration.cs src/LIAnsureProtect.Infrastructure/Persistence/Configurations/QuoteReferralFollowUpTaskConfiguration.cs src/LIAnsureProtect.Infrastructure/Persistence/Configurations/QuoteReferralTimelineEntryConfiguration.cs
```

- [ ] **Step 2: Drop the evidence FK (reference by id only)**

In `QuoteEvidenceRequestConfiguration.cs` delete the block:

```csharp
        builder.HasOne<QuoteReferralOperation>()
            .WithMany()
            .HasForeignKey(request => request.QuoteReferralOperationId)
            ...;
```

Keep the `QuoteReferralOperationId` property mapping (`quote_referral_operation_id`) as a plain column. Remove the now-unused `using LIAnsureProtect.Domain.Quotes;` only if nothing else in the file needs it.

- [ ] **Step 3: Build**

Run: `dotnet build LIAnsureProtect.slnx --no-restore`
Expected: PASS, 0/0.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor(quotes): remove legacy referral aggregate and evidence FK"
```

## Task C3: Add the DropReferralOperations migration (legacy schema)

**Files:**
- Create: migration under `src/LIAnsureProtect.Infrastructure/Persistence/Migrations/` (generated)

- [ ] **Step 1: Generate the migration**

Run:
```
dotnet ef migrations add DropReferralOperations --context SubmissionDbContext --project src/LIAnsureProtect.Infrastructure/LIAnsureProtect.Infrastructure.csproj --startup-project src/LIAnsureProtect.Api/LIAnsureProtect.Api.csproj
```
Expected: drops `quote_referral_operations`, `quote_referral_work_notes`, `quote_referral_follow_up_tasks`, `quote_referral_timeline_entries` from `public`, and drops the `quote_evidence_requests` → `quote_referral_operations` FK. Open the migration and confirm it does **not** drop the `quote_referral_operation_id` column (keep the id).

- [ ] **Step 2: Verify pending-model-changes clean for all three contexts**

Run all three `has-pending-model-changes` commands from the conventions block.
Expected: all report no pending changes.

- [ ] **Step 3: Commit**

```bash
git add src/LIAnsureProtect.Infrastructure/Persistence/Migrations
git commit -m "feat(quotes): drop legacy referral operations tables"
```

## Task C4: Full test pass + pending-model verification

- [ ] **Step 1: Build**

Run: `dotnet build LIAnsureProtect.slnx --no-restore`
Expected: PASS, 0/0.

- [ ] **Step 2: Full test run**

Run: `dotnet test LIAnsureProtect.slnx --no-build`
Expected: all UnitTests + IntegrationTests pass (1 PostgreSQL opt-in test skipped, as usual).

- [ ] **Step 3: Pending-model check (all three contexts)** — run the three commands; expect all clean.

- [ ] **Step 4: Commit (only if any test fixups were needed)**

```bash
git add -A
git commit -m "test: stabilize full suite after referral operations carve"
```

## Task C5: Docs — status, changelog, learnings, README

**Files:**
- Modify: `docs/project-status.md` (M36 status + verification lines; update "Recommended next milestone" to the evidence/decision slice)
- Modify: `CHANGELOG.md`
- Modify: `README.md` (M36 status paragraph + a learnings link)
- Create: `docs/dev/milestone-36-underwriting-referral-operations-learnings.md`
- Modify: `docs/architecture/overview.md` (touch the referral-operations location if it names the legacy path)

- [ ] **Step 1: Write the learnings doc**

Cover: the strangler order; events not ports (and why — your decision); the create read-back via the M35 reader port; the create-if-missing/self-heal idempotency + dedupe table; reads staying legacy behind one port + the new legacy→module-Application edge; the eventual-consistency test rework (pump the outbox); the dropped evidence FK; that no 4th context was needed; and what's deferred (evidence carve, the decision slice, M40 dispatcher decoupling). Reference `docs/dev/async-and-eventing-conventions.md`.

- [ ] **Step 2: Update status/changelog/readme/overview** per the existing milestone style (see M35's entries as the template).

- [ ] **Step 3: Commit**

```bash
git add docs README.md CHANGELOG.md
git commit -m "docs: record Milestone 36 underwriting referral operations"
```

## Task C6: Full local CI + PR

- [ ] **Step 1: Run full local CI**

Run: `pwsh ./scripts/run-local-ci.ps1`
Expected: green — applies all three contexts' migrations against fresh Docker PostgreSQL; frontend build/lint/test; artifact created.

- [ ] **Step 2: Push and open the PR**

```bash
git push -u origin feat/milestone-36-underwriting-referral-operations
gh pr create --base main --title "feat: carve referral operations into the Underwriting module" --body "<summary + verification + deferred items>"
```
Squash-merge when GitHub Actions CI + Claude review pass; then re-sync `main` and seed the M37 handoff.

---

## Self-Review

**Spec coverage:**
- Goal / atomic carve / no 4th context → Phase A (A9–A13), conventions block. ✓
- Event seam + second projector → A7, A11, B2, B3. ✓
- Create event data gap (read-back via M35 reader) → A6, A11. ✓
- Create/close/evidence via events → B2 (mapper), B4/B5/B6 (remove legacy writes). ✓
- Underwriter actions stay synchronous module commands → A8, B8. ✓
- Reads stay legacy behind one port (+ new edge + ratchet update) → A4, B7. ✓
- Consistency: create-if-missing + dedupe + eventual + test pump → A9, A11, B1, B9. ✓
- Evidence FK drop → C2. ✓
- Schema move drop-and-recreate (CreateReferralOperations + DropReferralOperations) → A13, C3. ✓
- Scripts/guard/CI unchanged → asserted (no 4th context); verified by C6. ✓
- Async/eventing conventions doc → already created; referenced in C5 learnings. ✓
- Test plan (aggregate move, projector tests, migration test, rework, ratchet) → A3, A13, B1, B3, B7, B9. ✓
- Verification gates → C4, C6. ✓
- Deferred items → C5 learnings. ✓

**Placeholder scan:** No "TBD/TODO/handle edge cases". The two `>`-noted decision points (accept-path double timeline entry; whether write-commands should self-heal) are explicit, scoped, and gated on a failing test — not open placeholders.

**Type consistency:** `IReferralOperationRepository` (GetByQuoteIdForUpdateAsync/ExistsForQuoteAsync/AddAsync/SaveChangesAsync), `IReferralOperationsReader` (GetSummariesAsync/GetTimelineAsync), `IReferralOperationProjector.ProjectAsync(ReferralOperationEvent)`, `ReferralOperationEventKind`, `ReferralQuoteContext(QuoteId,RiskTier,ReferredAtUtc,ExpiresAtUtc)`, and `CreateDefault(Guid, string, DateTime, DateTime)` are used consistently across A5/A6/A7/A8/A11/B2/B7. ✓
