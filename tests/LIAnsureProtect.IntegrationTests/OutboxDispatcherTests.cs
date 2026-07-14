using System.Diagnostics.Metrics;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Application.Quotes.Assurance;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Quotes;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Consumers;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.Notifications;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.ReferralOperations;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.Assurance;
using LIAnsureProtect.Infrastructure.Persistence.Outbox;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Domain;
using LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;
using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Modules.Underwriting.Application.Assurance;
using LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;
using LIAnsureProtect.Platform.Abstractions.Observability;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using ModuleEvidenceRequestCategory = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.EvidenceRequestCategory;
using ModuleEvidenceReviewDecisionStatus = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.EvidenceReviewDecisionStatus;
using ModuleOutboxMessage = LIAnsureProtect.Platform.Outbox.ModuleOutboxMessage;
using ModuleQuoteEvidenceRequestCreatedDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestCreatedDomainEvent;
using ModuleQuoteEvidenceRequestFollowUpSentDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestFollowUpSentDomainEvent;
using ModuleQuoteEvidenceRequestRemediationRequiredDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestRemediationRequiredDomainEvent;
using ModuleQuoteEvidenceRequestRespondedDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestRespondedDomainEvent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly QuoteAssuranceProjector quoteAssuranceProjector;
    private readonly QuoteAssuranceDecisionProjector quoteAssuranceDecisionProjector;

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
        quoteContextReaderStub
            .Setup(r => r.GetForAssuranceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuoteAssuranceRequirementContext?)null);

        projector = new NotificationInboxProjector(notificationsDbContext);
        referralProjector = new ReferralOperationProjector(
            underwritingDbContext,
            new EfReferralOperationRepository(underwritingDbContext, quoteContextReaderStub.Object));
        quoteAssuranceProjector = new QuoteAssuranceProjector(
            underwritingDbContext,
            quoteContextReaderStub.Object,
            new EfEvidenceRequestRepository(underwritingDbContext));
        quoteAssuranceDecisionProjector = new QuoteAssuranceDecisionProjector(dbContext);
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
    public async Task DispatchPendingMessagesAsync_Records_Processed_Message_Metric()
    {
        var processedMessages = 0L;
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == ObservabilityNames.MeterName
                && instrument.Name == ObservabilityNames.OutboxDispatchProcessedMessagesMetric)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == ObservabilityNames.OutboxDispatchProcessedMessagesMetric)
                Interlocked.Add(ref processedMessages, measurement);
        });
        meterListener.Start();

        var domainEvent = new SubmissionSubmittedDomainEvent(
            Guid.Parse("d5a5f4dd-061c-468e-b8f7-01999c4af901"),
            "test-user-1",
            new DateTime(2026, 7, 2, 2, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 7, 2, 2, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dispatcher = CreateDispatcher(new RecordingNotificationPublisher());

        await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        Assert.True(processedMessages >= 1);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Uses_Registered_Consumer_For_Matched_Message()
    {
        var domainEvent = new SubmissionSubmittedDomainEvent(
            Guid.Parse("5f801a6b-4fa8-44f3-859d-3f1663f6b801"),
            "test-user-1",
            new DateTime(2026, 7, 2, 1, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 7, 2, 1, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var consumer = new RecordingOutboxConsumer();
        var dispatcher = new OutboxDispatcher(
            [new SubmissionOutboxSource(dbContext)],
            [consumer],
            NullLogger<OutboxDispatcher>.Instance);

        var processedCount = await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var savedMessage = await dbContext.OutboxMessages.SingleAsync(
            message => message.Id == outboxMessage.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, processedCount);
        Assert.Equal([outboxMessage.Id], consumer.HandledMessages);
        Assert.NotNull(savedMessage.ProcessedAtUtc);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Publishes_Quote_Notification_Before_Marking_Message_Processed()
    {
        var domainEvent = new QuoteGeneratedDomainEvent(
            Guid.Parse("d9f7a2f5-0c3c-46f3-a841-ac26f8af1169"),
            Guid.Parse("a6f943ad-9c87-4932-9e65-8fdd97da4079"),
            "customer-1",
            QuoteStatus.Quoted,
            new DateTime(2026, 6, 21, 5, 0, 0, DateTimeKind.Utc),
            Version: 3,
            Premium: 12_345m,
            ExpiresAtUtc: new DateTime(2026, 7, 21, 5, 0, 0, DateTimeKind.Utc));
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
        Assert.Equal("3", publishedMessage.Attributes["version"]);
        Assert.Equal("12345", publishedMessage.Attributes["premium"]);
        Assert.Equal("2026-07-21T05:00:00.0000000Z", publishedMessage.Attributes["expiresAtUtc"]);
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
        var domainEvent = new ModuleQuoteEvidenceRequestCreatedDomainEvent(
            Guid.Parse("c3d23aa4-c01f-4ff4-851c-bd4c26ce1635"),
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            Guid.Parse("6d3f563f-595c-4ad6-90ef-5d7d75066763"),
            "customer-1",
            "underwriter-1",
            ModuleEvidenceRequestCategory.MultiFactorAuthentication,
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc),
            Title: "Verify privileged-account MFA",
            QuoteVersion: 2);
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
        Assert.Equal("Verify privileged-account MFA", publishedMessage.Attributes["requestTitle"]);
        Assert.Equal("2", publishedMessage.Attributes["quoteVersion"]);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Publishes_Evidence_Response_Notification_To_Underwriting()
    {
        var domainEvent = new ModuleQuoteEvidenceRequestRespondedDomainEvent(
            Guid.Parse("c3d23aa4-c01f-4ff4-851c-bd4c26ce1635"),
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            Guid.Parse("6d3f563f-595c-4ad6-90ef-5d7d75066763"),
            "customer-1",
            "underwriter-1",
            "customer-1",
            ModuleEvidenceRequestCategory.MultiFactorAuthentication,
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
        var domainEvent = new ModuleQuoteEvidenceRequestFollowUpSentDomainEvent(
            Guid.Parse("c3d23aa4-c01f-4ff4-851c-bd4c26ce1635"),
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            Guid.Parse("6d3f563f-595c-4ad6-90ef-5d7d75066763"),
            "customer-1",
            "underwriter-1",
            "underwriter-2",
            ModuleEvidenceRequestCategory.MultiFactorAuthentication,
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
        var domainEvent = new ModuleQuoteEvidenceRequestRemediationRequiredDomainEvent(
            Guid.Parse("c3d23aa4-c01f-4ff4-851c-bd4c26ce1635"),
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            Guid.Parse("6d3f563f-595c-4ad6-90ef-5d7d75066763"),
            "customer-1",
            "underwriter-1",
            "underwriter-2",
            ModuleEvidenceRequestCategory.MultiFactorAuthentication,
            ModuleEvidenceReviewDecisionStatus.NeedsClarification,
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
        var domainEvent = new ModuleQuoteEvidenceRequestRespondedDomainEvent(
            Guid.Parse("c3d23aa4-c01f-4ff4-851c-bd4c26ce1635"),
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            Guid.Parse("6d3f563f-595c-4ad6-90ef-5d7d75066763"),
            "customer-1",
            "underwriter-1",
            "customer-1",
            ModuleEvidenceRequestCategory.MultiFactorAuthentication,
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

    [Fact]
    public async Task DispatchPendingMessagesAsync_Drains_Both_Sources()
    {
        using var secondaryConnection = new SqliteConnection("DataSource=:memory:");
        secondaryConnection.Open();
        using var secondaryDbContext = new SubmissionDbContext(
            new DbContextOptionsBuilder<SubmissionDbContext>().UseSqlite(secondaryConnection).Options);
        secondaryDbContext.Database.EnsureCreated();

        var firstEvent = new QuoteGeneratedDomainEvent(
            Guid.Parse("936b174c-1394-46a7-8063-1103a6ac9bf4"),
            Guid.Parse("b9e152bc-3589-4c5a-b8af-83cc112609e8"),
            "customer-1",
            QuoteStatus.Quoted,
            new DateTime(2026, 6, 21, 5, 0, 0, DateTimeKind.Utc));
        var firstMessage = OutboxMessage.FromDomainEvent(
            firstEvent,
            new DateTime(2026, 6, 21, 5, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(firstMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var secondEvent = new QuoteGeneratedDomainEvent(
            Guid.Parse("a6cf84c9-a544-43e7-9f7d-7657861ee4e3"),
            Guid.Parse("bbfe1508-c030-4a91-b6f0-748187dd083d"),
            "customer-2",
            QuoteStatus.Quoted,
            new DateTime(2026, 6, 21, 5, 1, 0, DateTimeKind.Utc));
        var secondMessage = OutboxMessage.FromDomainEvent(
            secondEvent,
            new DateTime(2026, 6, 21, 5, 1, 5, DateTimeKind.Utc));
        await secondaryDbContext.OutboxMessages.AddAsync(secondMessage, TestContext.Current.CancellationToken);
        await secondaryDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisher = new RecordingNotificationPublisher();
        var dispatcher = CreateDispatcher(
            publisher,
            new SubmissionOutboxSource(dbContext),
            new SubmissionOutboxSource(secondaryDbContext));

        var processedCount = await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        secondaryDbContext.ChangeTracker.Clear();
        var savedFirst = await dbContext.OutboxMessages.SingleAsync(
            message => message.Id == firstMessage.Id,
            TestContext.Current.CancellationToken);
        var savedSecond = await secondaryDbContext.OutboxMessages.SingleAsync(
            message => message.Id == secondMessage.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, processedCount);
        Assert.Equal(2, publisher.PublishedMessages.Count);
        Assert.NotNull(savedFirst.ProcessedAtUtc);
        Assert.NotNull(savedSecond.ProcessedAtUtc);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Processes_Messages_In_CreatedAtUtc_Order_Across_Sources()
    {
        using var secondaryConnection = new SqliteConnection("DataSource=:memory:");
        secondaryConnection.Open();
        using var secondaryDbContext = new SubmissionDbContext(
            new DbContextOptionsBuilder<SubmissionDbContext>().UseSqlite(secondaryConnection).Options);
        secondaryDbContext.Database.EnsureCreated();

        var laterEvent = new QuoteGeneratedDomainEvent(
            Guid.Parse("6598dcbb-2953-44eb-96f2-84ca3598ca75"),
            Guid.Parse("dd3f4cfb-1f78-460f-b709-a120266cd9c3"),
            "customer-later",
            QuoteStatus.Quoted,
            new DateTime(2026, 6, 21, 5, 2, 0, DateTimeKind.Utc));
        var laterMessage = OutboxMessage.FromDomainEvent(
            laterEvent,
            new DateTime(2026, 6, 21, 5, 2, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(laterMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var earlierEvent = new QuoteGeneratedDomainEvent(
            Guid.Parse("f8bc3602-f5ea-4f1a-a063-2ee5f5f8b545"),
            Guid.Parse("403974b5-76ab-458d-85f7-cb4ef31f2751"),
            "customer-earlier",
            QuoteStatus.Quoted,
            new DateTime(2026, 6, 21, 5, 1, 0, DateTimeKind.Utc));
        var earlierMessage = OutboxMessage.FromDomainEvent(
            earlierEvent,
            new DateTime(2026, 6, 21, 5, 1, 5, DateTimeKind.Utc));
        await secondaryDbContext.OutboxMessages.AddAsync(earlierMessage, TestContext.Current.CancellationToken);
        await secondaryDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisher = new RecordingNotificationPublisher();
        var dispatcher = CreateDispatcher(
            publisher,
            new SubmissionOutboxSource(dbContext),
            new SubmissionOutboxSource(secondaryDbContext));

        await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Collection(
            publisher.PublishedMessages,
            message => Assert.Equal(earlierMessage.Id, message.OutboxMessageId),
            message => Assert.Equal(laterMessage.Id, message.OutboxMessageId));
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Processes_Module_Evidence_Before_Legacy_Decision_By_CreatedAtUtc()
    {
        var quoteId = Guid.Parse("833d61c3-9fa5-4e75-b9bb-a06354746265");
        var submissionId = Guid.Parse("b0d9017d-ef32-41df-9827-4658b2465e8a");
        var evidenceEvent = new ModuleQuoteEvidenceRequestCreatedDomainEvent(
            Guid.Parse("272a446f-465c-499c-9a60-78f264d816d2"),
            quoteId,
            submissionId,
            "customer-1",
            "underwriter-1",
            ModuleEvidenceRequestCategory.MultiFactorAuthentication,
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc));
        var evidenceMessage = ModuleOutboxMessage.FromDomainEvent(
            evidenceEvent,
            new DateTime(2026, 6, 22, 9, 0, 5, DateTimeKind.Utc));
        await underwritingDbContext.OutboxMessages.AddAsync(evidenceMessage, TestContext.Current.CancellationToken);
        await underwritingDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var decisionEvent = new QuoteUnderwritingDecisionRecordedDomainEvent(
            quoteId,
            submissionId,
            "customer-1",
            "underwriter-2",
            QuoteUnderwritingDecision.Approved,
            new DateTime(2026, 6, 22, 9, 1, 0, DateTimeKind.Utc));
        var decisionMessage = OutboxMessage.FromDomainEvent(
            decisionEvent,
            new DateTime(2026, 6, 22, 9, 1, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(decisionMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisher = new RecordingNotificationPublisher();
        var dispatcher = CreateDispatcher(
            publisher,
            new SubmissionOutboxSource(dbContext),
            new UnderwritingOutboxSource(underwritingDbContext));

        var processedCount = await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        underwritingDbContext.ChangeTracker.Clear();
        var savedEvidenceMessage = await underwritingDbContext.OutboxMessages.SingleAsync(
            message => message.Id == evidenceMessage.Id,
            TestContext.Current.CancellationToken);
        var savedDecisionMessage = await dbContext.OutboxMessages.SingleAsync(
            message => message.Id == decisionMessage.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, processedCount);
        Assert.Collection(
            publisher.PublishedMessages,
            message =>
            {
                Assert.Equal(evidenceMessage.Id, message.OutboxMessageId);
                Assert.Equal(NotificationMessageTypes.EvidenceRequestCreated, message.Type);
            },
            message =>
            {
                Assert.Equal(decisionMessage.Id, message.OutboxMessageId);
                Assert.Equal(NotificationMessageTypes.QuoteUnderwritingDecisionRecorded, message.Type);
            });
        Assert.NotNull(savedEvidenceMessage.ProcessedAtUtc);
        Assert.NotNull(savedDecisionMessage.ProcessedAtUtc);
    }

    [Fact]
    public async Task DispatchPendingMessagesAsync_Records_Retry_And_Continues_Batch_When_Consumer_Throws()
    {
        // First message: handled by a consumer that throws (simulating an unexpected crash).
        var submittedEvent = new SubmissionSubmittedDomainEvent(
            Guid.Parse("0b7cbb95-98a1-4f7d-b1cc-3ac6ff09b101"),
            "test-user-1",
            new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc));
        var throwingMessage = OutboxMessage.FromDomainEvent(
            submittedEvent,
            new DateTime(2026, 7, 2, 8, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(throwingMessage, TestContext.Current.CancellationToken);

        // Second message: not handled by the throwing consumer; must still be processed.
        var quoteEvent = new QuoteGeneratedDomainEvent(
            Guid.Parse("34e6ee9d-4a52-4f0f-b9e8-4e9dc80c2a02"),
            Guid.Parse("41b706ff-5e19-486e-b12f-0a742f5a2b03"),
            "customer-1",
            QuoteStatus.Quoted,
            new DateTime(2026, 7, 2, 8, 1, 0, DateTimeKind.Utc));
        var healthyMessage = OutboxMessage.FromDomainEvent(
            quoteEvent,
            new DateTime(2026, 7, 2, 8, 1, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(healthyMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publisher = new RecordingNotificationPublisher();
        var dispatcher = new OutboxDispatcher(
            [new SubmissionOutboxSource(dbContext)],
            [
                new ThrowingOutboxConsumer(),
                new NotificationOutboxMessageConsumer(CreateNotificationRegistry(), projector, publisher)
            ],
            NullLogger<OutboxDispatcher>.Instance);

        var processedCount = await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var savedThrowing = await dbContext.OutboxMessages.SingleAsync(
            message => message.Id == throwingMessage.Id,
            TestContext.Current.CancellationToken);
        var savedHealthy = await dbContext.OutboxMessages.SingleAsync(
            message => message.Id == healthyMessage.Id,
            TestContext.Current.CancellationToken);

        // The throwing message is left pending with retry metadata (a transient failure) ...
        Assert.Equal(1, processedCount);
        Assert.Null(savedThrowing.ProcessedAtUtc);
        Assert.Equal(1, savedThrowing.PublishAttemptCount);
        Assert.NotNull(savedThrowing.NextAttemptAtUtc);
        Assert.Null(savedThrowing.FailedAtUtc);
        Assert.Contains(nameof(ThrowingOutboxConsumer), savedThrowing.Error);

        // ... while the healthy message in the same batch is still processed and saved.
        Assert.NotNull(savedHealthy.ProcessedAtUtc);
        Assert.Single(publisher.PublishedMessages);
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
        return CreateDispatcher(publisher, new SubmissionOutboxSource(dbContext));
    }

    private OutboxDispatcher CreateDispatcher(
        INotificationPublisher publisher,
        params IOutboxSource[] sources)
    {
        return new OutboxDispatcher(
            sources,
            [
                new ReferralOperationOutboxMessageConsumer(CreateReferralRegistry(), referralProjector),
                new QuoteAssuranceOutboxMessageConsumer(
                    new OutboxMessageMapperRegistry<QuoteAssuranceEvent>([new QuoteGeneratedAssuranceMapper()]),
                    quoteAssuranceProjector),
                new QuoteAssuranceDecisionOutboxMessageConsumer(
                    new OutboxMessageMapperRegistry<QuoteAssuranceDecisionEvent>(
                    [
                        new EvidenceAcceptedAssuranceDecisionMapper(),
                        new EvidenceRemediationAssuranceDecisionMapper()
                    ]),
                    quoteAssuranceDecisionProjector),
                new NotificationOutboxMessageConsumer(CreateNotificationRegistry(), projector, publisher)
            ],
            NullLogger<OutboxDispatcher>.Instance);
    }

    private static OutboxMessageMapperRegistry<NotificationMessage> CreateNotificationRegistry()
    {
        return new OutboxMessageMapperRegistry<NotificationMessage>(
            [
                new QuoteGeneratedNotificationMapper(),
                new QuoteUnderwritingDecisionRecordedNotificationMapper(),
                new QuoteAcceptedNotificationMapper(),
                new PolicyBoundNotificationMapper(),
                new EvidenceRequestCreatedNotificationMapper(),
                new EvidenceRequestRespondedNotificationMapper(),
                new EvidenceRequestAcceptedNotificationMapper(),
                new EvidenceRequestCancelledNotificationMapper(),
                new EvidenceRequestFollowUpSentNotificationMapper(),
                new EvidenceRequestRemediationRequiredNotificationMapper()
            ]);
    }

    private static OutboxMessageMapperRegistry<LIAnsureProtect.Modules.Underwriting.Application.Referrals.ReferralOperationEvent>
        CreateReferralRegistry()
    {
        return new OutboxMessageMapperRegistry<LIAnsureProtect.Modules.Underwriting.Application.Referrals.ReferralOperationEvent>(
            [
                new QuoteGeneratedReferralOperationMapper(),
                new QuoteUnderwritingDecisionReferralOperationMapper(),
                new EvidenceRequestCreatedReferralOperationMapper(),
                new EvidenceRequestRespondedReferralOperationMapper(),
                new EvidenceRequestAcceptedReferralOperationMapper(),
                new EvidenceRequestCancelledReferralOperationMapper(),
                new EvidenceRequestFollowUpSentReferralOperationMapper(),
                new EvidenceRequestRemediationRequiredReferralOperationMapper()
            ]);
    }

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

    private sealed class ThrowingOutboxConsumer : IOutboxMessageConsumer
    {
        public Task<OutboxMessageConsumerResult> ConsumeAsync(
            IOutboxMessageView outboxMessage,
            DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            if (outboxMessage.Type != nameof(SubmissionSubmittedDomainEvent))
                return Task.FromResult(OutboxMessageConsumerResult.NotHandled());

            throw new InvalidOperationException("Simulated consumer crash.");
        }
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
