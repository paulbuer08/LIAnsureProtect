using LIAnsureProtect.Application.Notifications;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Persistence.Outbox;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.IntegrationTests;

public sealed class OutboxDispatcherTests : IDisposable
{
    private readonly SqliteConnection databaseConnection;
    private readonly SubmissionDbContext dbContext;

    public OutboxDispatcherTests()
    {
        databaseConnection = new SqliteConnection("DataSource=:memory:");
        databaseConnection.Open();

        var dbContextOptions = new DbContextOptionsBuilder<SubmissionDbContext>()
            .UseSqlite(databaseConnection)
            .Options;

        dbContext = new SubmissionDbContext(dbContextOptions);
        dbContext.Database.EnsureCreated();
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
        var dispatcher = new OutboxDispatcher(dbContext, publisher);

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
        var dispatcher = new OutboxDispatcher(dbContext, publisher);

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
        var dispatcher = new OutboxDispatcher(dbContext, publisher);

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
        var dispatcher = new OutboxDispatcher(dbContext, publisher);

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
        var dispatcher = new OutboxDispatcher(dbContext, publisher);

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
        var dispatcher = new OutboxDispatcher(dbContext, publisher);

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
        var dispatcher = new OutboxDispatcher(dbContext, publisher);

        await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        var publishedMessage = Assert.Single(publisher.PublishedMessages);
        Assert.Equal(NotificationMessageTypes.EvidenceRequestFollowUpSent, publishedMessage.Type);
        Assert.Equal(NotificationAudiences.CustomerOrBroker, publishedMessage.Audience);
        Assert.Equal("underwriter-2", publishedMessage.Attributes["followedUpByUserId"]);
    }

    public void Dispose()
    {
        dbContext.Dispose();
        databaseConnection.Dispose();
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
