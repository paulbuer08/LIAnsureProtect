using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LIAnsureProtect.IntegrationTests.Underwriting;

/// <summary>
/// Characterisation tests for <see cref="ReferralOperationProjector"/> covering idempotency on
/// <see cref="ReferralOperationEvent.SourceOutboxMessageId"/> and the create-if-missing / self-heal
/// behaviour. Uses an in-memory SQLite <see cref="UnderwritingDbContext"/> and a Moq
/// <see cref="IUnderwritingQuoteContextReader"/>.
/// </summary>
public sealed class ReferralOperationProjectorTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly UnderwritingDbContext dbContext;
    private readonly Mock<IUnderwritingQuoteContextReader> quoteContextReaderMock;

    private readonly Guid quoteId = Guid.NewGuid();
    private readonly DateTime referredAt = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
    private readonly DateTime expiresAt = new(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);

    public ReferralOperationProjectorTests()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<UnderwritingDbContext>()
            .UseSqlite(connection)
            .Options;

        dbContext = new UnderwritingDbContext(options);
        dbContext.Database.EnsureCreated();

        quoteContextReaderMock = new Mock<IUnderwritingQuoteContextReader>();
        quoteContextReaderMock
            .Setup(reader => reader.GetForReferralOperationAsync(quoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReferralQuoteContext(quoteId, "High", referredAt, expiresAt));
    }

    [Fact]
    public async Task Created_event_creates_operation_once_and_is_idempotent()
    {
        // Arrange
        var sourceOutboxMessageId = Guid.NewGuid();
        var createdEvent = new ReferralOperationEvent(
            SourceOutboxMessageId: sourceOutboxMessageId,
            Kind: ReferralOperationEventKind.Created,
            QuoteId: quoteId,
            ActorUserId: "system",
            OccurredAtUtc: referredAt,
            EvidenceRequestId: null,
            Decision: null);

        var projector = new ReferralOperationProjector(dbContext, quoteContextReaderMock.Object);

        // Act — apply the Created event twice with the same SourceOutboxMessageId.
        await projector.ProjectAsync(createdEvent, TestContext.Current.CancellationToken);
        await projector.ProjectAsync(createdEvent, TestContext.Current.CancellationToken);

        // Assert — exactly one operation and one dedupe marker, not two.
        dbContext.ChangeTracker.Clear();

        var operations = await dbContext.QuoteReferralOperations.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(operations);
        Assert.Equal(quoteId, operations[0].QuoteId);

        var markers = await dbContext.ReferralOperationProjectedMessages.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(markers);
        Assert.Equal(sourceOutboxMessageId, markers[0].SourceOutboxMessageId);

        // The quote-context reader must only have been called once (second call was a no-op).
        quoteContextReaderMock.Verify(
            reader => reader.GetForReferralOperationAsync(quoteId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Decision_event_self_heals_when_operation_missing_then_closes()
    {
        // Arrange — no operation exists yet; the projector must create it and then close it.
        var decisionEvent = new ReferralOperationEvent(
            SourceOutboxMessageId: Guid.NewGuid(),
            Kind: ReferralOperationEventKind.DecisionRecorded,
            QuoteId: quoteId,
            ActorUserId: "underwriter-1",
            OccurredAtUtc: referredAt.AddDays(1),
            EvidenceRequestId: null,
            Decision: "Approved");

        var projector = new ReferralOperationProjector(dbContext, quoteContextReaderMock.Object);

        // Act
        await projector.ProjectAsync(decisionEvent, TestContext.Current.CancellationToken);

        // Assert — operation was self-healed (created) and then closed in a single call.
        dbContext.ChangeTracker.Clear();

        var operation = await dbContext.QuoteReferralOperations
            .Include(op => op.TimelineEntries)
            .SingleAsync(op => op.QuoteId == quoteId, TestContext.Current.CancellationToken);

        Assert.Equal(ReferralOperationStatus.Closed, operation.Status);
        Assert.NotNull(operation.ClosedAtUtc);

        // Timeline should have OperationCreated (from CreateDefault) + StatusChanged (from CloseForDecision).
        Assert.Contains(operation.TimelineEntries,
            entry => entry.EntryType == ReferralTimelineEntryType.OperationCreated);
        Assert.Contains(operation.TimelineEntries,
            entry => entry.EntryType == ReferralTimelineEntryType.StatusChanged);

        // Reader was called exactly once to self-heal.
        quoteContextReaderMock.Verify(
            reader => reader.GetForReferralOperationAsync(quoteId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Duplicate_source_message_id_is_a_no_op()
    {
        // Arrange — apply a DecisionRecorded event once (closing the operation), then replay the
        // same SourceOutboxMessageId. The dedupe marker must prevent a second Apply call, so the
        // timeline grows by only one StatusChanged entry (not two), and no exception is thrown.
        var sourceOutboxMessageId = Guid.NewGuid();
        var decisionEvent = new ReferralOperationEvent(
            SourceOutboxMessageId: sourceOutboxMessageId,
            Kind: ReferralOperationEventKind.DecisionRecorded,
            QuoteId: quoteId,
            ActorUserId: "underwriter-1",
            OccurredAtUtc: referredAt.AddDays(1),
            EvidenceRequestId: null,
            Decision: "Declined");

        var projector = new ReferralOperationProjector(dbContext, quoteContextReaderMock.Object);

        // Act — apply the same DecisionRecorded event twice.
        await projector.ProjectAsync(decisionEvent, TestContext.Current.CancellationToken);
        await projector.ProjectAsync(decisionEvent, TestContext.Current.CancellationToken);

        // Assert — only one marker persisted (idempotency).
        dbContext.ChangeTracker.Clear();

        var markers = await dbContext.ReferralOperationProjectedMessages.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(markers);
        Assert.Equal(sourceOutboxMessageId, markers[0].SourceOutboxMessageId);

        // Only one StatusChanged entry from CloseForDecision (not duplicated).
        var operation = await dbContext.QuoteReferralOperations
            .Include(op => op.TimelineEntries)
            .SingleAsync(op => op.QuoteId == quoteId, TestContext.Current.CancellationToken);

        var statusChangedEntries = operation.TimelineEntries
            .Where(entry => entry.EntryType == ReferralTimelineEntryType.StatusChanged)
            .ToList();

        Assert.Single(statusChangedEntries);
        Assert.Equal(ReferralOperationStatus.Closed, operation.Status);
    }

    public void Dispose()
    {
        dbContext.Dispose();
        connection.Dispose();
    }
}
