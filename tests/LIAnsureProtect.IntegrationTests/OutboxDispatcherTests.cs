using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Persistence.Outbox;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Domain;
using LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;
using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LIAnsureProtect.IntegrationTests;

public sealed class OutboxDispatcherTests : IDisposable
{
    private readonly SqliteConnection submissionConnection;
    private readonly SqliteConnection notificationsConnection;
    private readonly SqliteConnection underwritingConnection;
    private readonly SubmissionDbContext dbContext;
    private readonly NotificationsDbContext notificationsDbContext;
    private readonly UnderwritingDbContext underwritingDbContext;
    private readonly NotificationInboxProjector projector;
    private readonly ReferralOperationProjector referralProjector;

    public OutboxDispatcherTests()
    {
        // The outbox lives in the Submissions/legacy context; the inbox lives in the Notifications
        // module's own context (its own schema). They are separate databases here, mirroring the
        // idempotent-ordered projection that needs no shared transaction.
        submissionConnection = new SqliteConnection("DataSource=:memory:");
        submissionConnection.Open();
        dbContext = new SubmissionDbContext(
            new DbContextOptionsBuilder<SubmissionDbContext>().UseSqlite(submissionConnection).Options);
        dbContext.Database.EnsureCreated();

        notificationsConnection = new SqliteConnection("DataSource=:memory:");
        notificationsConnection.Open();
        notificationsDbContext = new NotificationsDbContext(
            new DbContextOptionsBuilder<NotificationsDbContext>().UseSqlite(notificationsConnection).Options);
        notificationsDbContext.Database.EnsureCreated();

        // The referral projector uses the Underwriting module's own context (separate schema).
        underwritingConnection = new SqliteConnection("DataSource=:memory:");
        underwritingConnection.Open();
        underwritingDbContext = new UnderwritingDbContext(
            new DbContextOptionsBuilder<UnderwritingDbContext>().UseSqlite(underwritingConnection).Options);
        underwritingDbContext.Database.EnsureCreated();

        // A stub quote-context reader — the dispatcher tests don't exercise referral-create paths,
        // so returning null is safe; the projector's create-if-missing guard silently skips when null.
        var quoteContextReaderStub = new Mock<IUnderwritingQuoteContextReader>();
        quoteContextReaderStub
            .Setup(r => r.GetForReferralOperationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LIAnsureProtect.Modules.Underwriting.Application.ReferralQuoteContext?)null);

        projector = new NotificationInboxProjector(notificationsDbContext);
        referralProjector = new ReferralOperationProjector(
            underwritingDbContext,
            new EfReferralOperationRepository(underwritingDbContext, quoteContextReaderStub.Object));
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Marks_Pending_Outbox_Message_Processed()
    {
        var domainEvent = new SubmissionSubmittedDomainEvent(
            Guid.Parse("6f489e91-6a6b-4cc8-bc20-c63985f2a501"),
            "test-user-1",
            new DateTime(2026, 6, 20, 1, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 6, 20, 1, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisher = new RecordingNotificationPublisher();
        var dispatcher = CreateDispatcher(publisher);

        var processedCount = await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var savedMessage = await dbContext.OutboxMessages.SingleAsync(
            message => message.Id == outboxMessage.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, processedCount);
        Assert.NotNull(savedMessage.ProcessedAtUtc);
        Assert.Null(savedMessage.Error);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Publishes_Quote_Notification_Before_Marking_Message_Processed()
    {
        var domainEvent = new QuoteGeneratedDomainEvent(
            Guid.Parse("d9f7a2f5-0c3c-46f3-a841-ac26f8af1169"),
            Guid.Parse("a6f943ad-9c87-4932-9e65-8fdd97da4079"),
            "customer-1",
            QuoteStatus.Quoted,
            new DateTime(2026, 6, 21, 5, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 6, 21, 5, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisher = new RecordingNotificationPublisher();
        var dispatcher = CreateDispatcher(publisher);

        var processedCount = await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var savedMessage = await dbContext.OutboxMessages.SingleAsync(
            message => message.Id == outboxMessage.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, processedCount);
        var publishedMessage = Assert.Single(publisher.PublishedMessages);
        Assert.Equal(outboxMessage.Id.ToString("N"), publishedMessage.MessageId);
        Assert.Equal(NotificationMessageTypes.QuoteReady, publishedMessage.Type);
        Assert.Equal(NotificationAudiences.CustomerOrBroker, publishedMessage.Audience);
        Assert.Equal("customer-1", publishedMessage.OwnerUserId);
        Assert.Equal(outboxMessage.Id, publishedMessage.OutboxMessageId);
        Assert.NotNull(savedMessage.ProcessedAtUtc);
        Assert.Equal(1, savedMessage.PublishAttemptCount);
        Assert.Equal("local-provider-message-1", savedMessage.ProviderMessageId);
        Assert.Null(savedMessage.NextAttemptAtUtc);
        Assert.Null(savedMessage.FailedAtUtc);
        Assert.Null(savedMessage.Error);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Leaves_Message_Pending_For_Retry_When_Notification_Publish_Fails()
    {
        var domainEvent = new QuoteUnderwritingDecisionRecordedDomainEvent(
            Guid.Parse("fd61c036-06eb-46d0-a811-cef15408dd8e"),
            Guid.Parse("1aaf4497-3ba4-4f0b-8c5d-bf8f97474cb7"),
            "customer-1",
            "underwriter-1",
            QuoteUnderwritingDecision.Approved,
            new DateTime(2026, 6, 21, 6, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 6, 21, 6, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisher = new RecordingNotificationPublisher(
            NotificationPublishResult.TransientFailure("local notification provider is unavailable"));
        var dispatcher = CreateDispatcher(publisher);

        var processedCount = await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var savedMessage = await dbContext.OutboxMessages.SingleAsync(
            message => message.Id == outboxMessage.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, processedCount);
        Assert.Single(publisher.PublishedMessages);
        Assert.Null(savedMessage.ProcessedAtUtc);
        Assert.Equal(1, savedMessage.PublishAttemptCount);
        Assert.NotNull(savedMessage.LastPublishAttemptAtUtc);
        Assert.NotNull(savedMessage.NextAttemptAtUtc);
        Assert.Null(savedMessage.FailedAtUtc);
        Assert.Equal("local notification provider is unavailable", savedMessage.Error);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Records_Poison_Failure_When_Notification_Publish_Permanently_Fails()
    {
        var domainEvent = new QuoteAcceptedDomainEvent(
            Guid.Parse("d5ea7781-c981-4d08-967b-83369343a626"),
            Guid.Parse("426c2ac3-bf10-47af-a4ca-36825a354b14"),
            "customer-1",
            "customer-1",
            new DateTime(2026, 6, 21, 7, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 6, 21, 7, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisher = new RecordingNotificationPublisher(
            NotificationPublishResult.PermanentFailure("notification payload is not accepted by provider"));
        var dispatcher = CreateDispatcher(publisher);

        var processedCount = await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var savedMessage = await dbContext.OutboxMessages.SingleAsync(
            message => message.Id == outboxMessage.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, processedCount);
        var publishedMessage = Assert.Single(publisher.PublishedMessages);
        Assert.Equal(NotificationMessageTypes.QuoteAccepted, publishedMessage.Type);
        Assert.Equal(NotificationAudiences.BindingOperations, publishedMessage.Audience);
        Assert.Null(savedMessage.ProcessedAtUtc);
        Assert.Equal(1, savedMessage.PublishAttemptCount);
        Assert.NotNull(savedMessage.LastPublishAttemptAtUtc);
        Assert.Null(savedMessage.NextAttemptAtUtc);
        Assert.NotNull(savedMessage.FailedAtUtc);
        Assert.Equal("notification payload is not accepted by provider", savedMessage.Error);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Publishes_Evidence_Request_Created_Notification_To_Owner()
    {
        var domainEvent = new QuoteEvidenceRequestCreatedDomainEvent(
            Guid.Parse("c3d23aa4-c01f-4ff4-851c-bd4c26ce1635"),
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            Guid.Parse("6d3f563f-595c-4ad6-90ef-5d7d75066763"),
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.MultiFactorAuthentication,
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 6, 22, 9, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisher = new RecordingNotificationPublisher();
        var dispatcher = CreateDispatcher(publisher);

        await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        var publishedMessage = Assert.Single(publisher.PublishedMessages);
        Assert.Equal(NotificationMessageTypes.EvidenceRequestCreated, publishedMessage.Type);
        Assert.Equal(NotificationAudiences.CustomerOrBroker, publishedMessage.Audience);
        Assert.Equal("customer-1", publishedMessage.OwnerUserId);
        Assert.Equal("evidence-request", publishedMessage.SubjectReferenceType);
        Assert.Equal(domainEvent.EvidenceRequestId.ToString(), publishedMessage.SubjectReferenceId);
        Assert.Equal("underwriter-1", publishedMessage.Attributes["requestedByUserId"]);
        Assert.Equal("MultiFactorAuthentication", publishedMessage.Attributes["category"]);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Publishes_Evidence_Response_Notification_To_Underwriting()
    {
        var domainEvent = new QuoteEvidenceRequestRespondedDomainEvent(
            Guid.Parse("c3d23aa4-c01f-4ff4-851c-bd4c26ce1635"),
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            Guid.Parse("6d3f563f-595c-4ad6-90ef-5d7d75066763"),
            "customer-1",
            "underwriter-1",
            "customer-1",
            EvidenceRequestCategory.MultiFactorAuthentication,
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 6, 22, 12, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisher = new RecordingNotificationPublisher();
        var dispatcher = CreateDispatcher(publisher);

        await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        var publishedMessage = Assert.Single(publisher.PublishedMessages);
        Assert.Equal(NotificationMessageTypes.EvidenceRequestResponded, publishedMessage.Type);
        Assert.Equal(NotificationAudiences.UnderwritingOperations, publishedMessage.Audience);
        Assert.Equal("customer-1", publishedMessage.OwnerUserId);
        Assert.Equal("underwriter-1", publishedMessage.Attributes["requestedByUserId"]);
        Assert.Equal("customer-1", publishedMessage.Attributes["respondedByUserId"]);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Publishes_Evidence_Follow_Up_Notification_To_Owner()
    {
        var domainEvent = new QuoteEvidenceRequestFollowUpSentDomainEvent(
            Guid.Parse("c3d23aa4-c01f-4ff4-851c-bd4c26ce1635"),
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            Guid.Parse("6d3f563f-595c-4ad6-90ef-5d7d75066763"),
            "customer-1",
            "underwriter-1",
            "underwriter-2",
            EvidenceRequestCategory.MultiFactorAuthentication,
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 26, 9, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 6, 26, 9, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisher = new RecordingNotificationPublisher();
        var dispatcher = CreateDispatcher(publisher);

        await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        var publishedMessage = Assert.Single(publisher.PublishedMessages);
        Assert.Equal(NotificationMessageTypes.EvidenceRequestFollowUpSent, publishedMessage.Type);
        Assert.Equal(NotificationAudiences.CustomerOrBroker, publishedMessage.Audience);
        Assert.Equal("underwriter-2", publishedMessage.Attributes["followedUpByUserId"]);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Publishes_Evidence_Remediation_Required_Notification_To_Owner()
    {
        var domainEvent = new QuoteEvidenceRequestRemediationRequiredDomainEvent(
            Guid.Parse("c3d23aa4-c01f-4ff4-851c-bd4c26ce1635"),
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            Guid.Parse("6d3f563f-595c-4ad6-90ef-5d7d75066763"),
            "customer-1",
            "underwriter-1",
            "underwriter-2",
            EvidenceRequestCategory.MultiFactorAuthentication,
            EvidenceReviewDecisionStatus.NeedsClarification,
            "The response does not confirm privileged account MFA scope.",
            "Please confirm whether MFA applies to all administrator and service-owner accounts.",
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 14, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 6, 22, 14, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisher = new RecordingNotificationPublisher();
        var dispatcher = CreateDispatcher(publisher);

        var processedCount = await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var savedMessage = await dbContext.OutboxMessages.SingleAsync(
            message => message.Id == outboxMessage.Id,
            TestContext.Current.CancellationToken);
        var publishedMessage = Assert.Single(publisher.PublishedMessages);

        Assert.Equal(1, processedCount);
        Assert.Equal(NotificationMessageTypes.EvidenceRequestRemediationRequired, publishedMessage.Type);
        Assert.Equal(NotificationAudiences.CustomerOrBroker, publishedMessage.Audience);
        Assert.Equal("customer-1", publishedMessage.OwnerUserId);
        Assert.Equal("evidence-request", publishedMessage.SubjectReferenceType);
        Assert.Equal(domainEvent.EvidenceRequestId.ToString(), publishedMessage.SubjectReferenceId);
        Assert.Equal("underwriter-1", publishedMessage.Attributes["requestedByUserId"]);
        Assert.Equal("underwriter-2", publishedMessage.Attributes["reviewedByUserId"]);
        Assert.Equal("NeedsClarification", publishedMessage.Attributes["decision"]);
        Assert.Equal("The response does not confirm privileged account MFA scope.", publishedMessage.Attributes["reviewReason"]);
        Assert.Equal("Please confirm whether MFA applies to all administrator and service-owner accounts.", publishedMessage.Attributes["remediationGuidance"]);
        Assert.Equal("true", publishedMessage.Attributes["actionRequired"]);
        Assert.Equal("local-provider-message-1", savedMessage.ProviderMessageId);
        Assert.NotNull(savedMessage.ProcessedAtUtc);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Writes_Inbox_Entry_For_Customer_Notification()
    {
        var domainEvent = new QuoteGeneratedDomainEvent(
            Guid.Parse("d9f7a2f5-0c3c-46f3-a841-ac26f8af1169"),
            Guid.Parse("a6f943ad-9c87-4932-9e65-8fdd97da4079"),
            "customer-1",
            QuoteStatus.Quoted,
            new DateTime(2026, 6, 21, 5, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 6, 21, 5, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dispatcher = CreateDispatcher(new RecordingNotificationPublisher());
        await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        notificationsDbContext.ChangeTracker.Clear();
        var entry = await notificationsDbContext.NotificationInboxEntries
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("customer-1", entry.RecipientUserId);
        Assert.Equal(NotificationMessageTypes.QuoteReady, entry.Type);
        Assert.Equal(NotificationAudiences.CustomerOrBroker, entry.Audience);
        Assert.Equal(outboxMessage.Id, entry.SourceOutboxMessageId);
        Assert.Null(entry.ReadAtUtc);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Does_Not_Write_Inbox_Entry_For_Operations_Notification()
    {
        var domainEvent = new QuoteEvidenceRequestRespondedDomainEvent(
            Guid.Parse("c3d23aa4-c01f-4ff4-851c-bd4c26ce1635"),
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            Guid.Parse("6d3f563f-595c-4ad6-90ef-5d7d75066763"),
            "customer-1",
            "underwriter-1",
            "customer-1",
            EvidenceRequestCategory.MultiFactorAuthentication,
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 6, 22, 12, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dispatcher = CreateDispatcher(new RecordingNotificationPublisher());
        await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        notificationsDbContext.ChangeTracker.Clear();
        // An operations-addressed event produces no PERSONAL inbox entry ...
        var personalEntries = await notificationsDbContext.NotificationInboxEntries
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(personalEntries);

        // ... but does produce a shared TEAM entry for the underwriting-operations audience.
        var teamEntry = await notificationsDbContext.TeamNotificationEntries
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(NotificationAudiences.UnderwritingOperations, teamEntry.Audience);
        Assert.Equal(outboxMessage.Id, teamEntry.SourceOutboxMessageId);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Does_Not_Duplicate_Existing_Inbox_Entry()
    {
        var domainEvent = new QuoteGeneratedDomainEvent(
            Guid.Parse("d9f7a2f5-0c3c-46f3-a841-ac26f8af1169"),
            Guid.Parse("a6f943ad-9c87-4932-9e65-8fdd97da4079"),
            "customer-1",
            QuoteStatus.Quoted,
            new DateTime(2026, 6, 21, 5, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 6, 21, 5, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Pre-existing inbox entry for this outbox message (as if a prior dispatch attempt wrote it).
        var existingEntry = NotificationInboxEntry.Create(
            "customer-1",
            NotificationAudiences.CustomerOrBroker,
            NotificationMessageTypes.QuoteReady,
            "quote",
            domainEvent.QuoteId.ToString(),
            "{}",
            outboxMessage.Id,
            domainEvent.OccurredAtUtc,
            new DateTime(2026, 6, 21, 5, 0, 10, DateTimeKind.Utc));
        await notificationsDbContext.NotificationInboxEntries.AddAsync(existingEntry, TestContext.Current.CancellationToken);
        await notificationsDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dispatcher = CreateDispatcher(new RecordingNotificationPublisher());
        await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        notificationsDbContext.ChangeTracker.Clear();
        var entries = await notificationsDbContext.NotificationInboxEntries
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(entries);
    }

    public void Dispose()
    {
        dbContext.Dispose();
        notificationsDbContext.Dispose();
        underwritingDbContext.Dispose();
        submissionConnection.Dispose();
        notificationsConnection.Dispose();
        underwritingConnection.Dispose();
    }

    private OutboxDispatcher CreateDispatcher(INotificationPublisher publisher)
    {
        return new OutboxDispatcher(
            [new SubmissionOutboxSource(dbContext)],
            projector,
            publisher,
            referralProjector);
    }

    private sealed class RecordingNotificationPublisher(
        NotificationPublishResult? result = null) : INotificationPublisher
    {
        public List<NotificationMessage> PublishedMessages { get; } = [];

        public Task<NotificationPublishResult> PublishAsync(
            NotificationMessage message,
            CancellationToken cancellationToken)
        {
            PublishedMessages.Add(message);

            return Task.FromResult(result
                ?? NotificationPublishResult.Success("local-provider-message-1"));
        }
    }
}
