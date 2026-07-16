using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Domain;
using LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.IntegrationTests.Notifications;

/// <summary>
/// Covers the team inbox: the projector persists a single shared entry per team audience (idempotent),
/// and read state is independent per user (per-user read receipts), gated by allowed audiences.
/// </summary>
public sealed class TeamNotificationInboxTests : IDisposable
{
    private static readonly string[] OpsAudiences =
        [NotificationAudiences.UnderwritingOperations, NotificationAudiences.BindingOperations];

    private readonly SqliteConnection connection;
    private readonly NotificationsDbContext dbContext;
    private readonly NotificationInboxProjector projector;
    private readonly EfTeamNotificationRepository repository;

    public TeamNotificationInboxTests()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        dbContext = new NotificationsDbContext(
            new DbContextOptionsBuilder<NotificationsDbContext>().UseSqlite(connection).Options);
        dbContext.Database.EnsureCreated();
        projector = new NotificationInboxProjector(dbContext);
        repository = new EfTeamNotificationRepository(dbContext);
    }

    [Fact]
    public async Task Projector_Persists_One_Shared_Team_Entry_Idempotently()
    {
        var message = TeamMessage(Guid.Parse("b1a7d4e0-0000-4000-8000-000000000001"));

        await projector.ProjectAsync(message, TestContext.Current.CancellationToken);
        await projector.ProjectAsync(message, TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var entry = await dbContext.TeamNotificationEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(NotificationAudiences.UnderwritingOperations, entry.Audience);
        Assert.Equal(message.OutboxMessageId, entry.SourceOutboxMessageId);
    }

    [Fact]
    public async Task Read_State_Is_Per_User()
    {
        await projector.ProjectAsync(
            TeamMessage(Guid.Parse("b1a7d4e0-0000-4000-8000-000000000002")),
            TestContext.Current.CancellationToken);

        var forUnderwriterA = await repository.ListForAudiencesAsync(
            "uw-a", OpsAudiences, TestContext.Current.CancellationToken);
        var item = Assert.Single(forUnderwriterA);
        Assert.Equal(NotificationScopes.Team, item.Scope);
        Assert.Equal(NotificationAudiences.UnderwritingOperations, item.Audience);
        Assert.False(item.IsRead);

        var marked = await repository.MarkReadAsync(
            item.NotificationId, "uw-a", OpsAudiences, DateTime.UtcNow, TestContext.Current.CancellationToken);
        Assert.True(marked);

        dbContext.ChangeTracker.Clear();

        // uw-a now sees it read with no unread count ...
        Assert.True(Assert.Single(await repository.ListForAudiencesAsync(
            "uw-a", OpsAudiences, TestContext.Current.CancellationToken)).IsRead);
        Assert.Equal(0, await repository.CountUnreadForAudiencesAsync(
            "uw-a", OpsAudiences, TestContext.Current.CancellationToken));

        // ... while uw-b still sees the same shared entry as unread.
        Assert.False(Assert.Single(await repository.ListForAudiencesAsync(
            "uw-b", OpsAudiences, TestContext.Current.CancellationToken)).IsRead);
        Assert.Equal(1, await repository.CountUnreadForAudiencesAsync(
            "uw-b", OpsAudiences, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Mark_Read_Is_Rejected_When_The_Audience_Is_Not_Allowed()
    {
        await projector.ProjectAsync(
            TeamMessage(Guid.Parse("b1a7d4e0-0000-4000-8000-000000000003")),
            TestContext.Current.CancellationToken);
        var entryId = (await dbContext.TeamNotificationEntries.SingleAsync(TestContext.Current.CancellationToken)).Id;

        // No allowed audiences (e.g. a customer) -> cannot mark a team entry by guessing its id.
        Assert.False(await repository.MarkReadAsync(
            entryId, "cust-1", [], DateTime.UtcNow, TestContext.Current.CancellationToken));

        // Allowed audiences that do not include the entry's audience -> still rejected.
        Assert.False(await repository.MarkReadAsync(
            entryId, "x", [NotificationAudiences.BindingOperations], DateTime.UtcNow, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task List_Returns_Nothing_When_The_Caller_Has_No_Team_Audiences()
    {
        await projector.ProjectAsync(
            TeamMessage(Guid.Parse("b1a7d4e0-0000-4000-8000-000000000004")),
            TestContext.Current.CancellationToken);

        Assert.Empty(await repository.ListForAudiencesAsync(
            "cust-1", [], TestContext.Current.CancellationToken));
        Assert.Equal(0, await repository.CountUnreadForAudiencesAsync(
            "cust-1", [], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task New_quote_version_marks_prior_notifications_historical_without_faking_read_state()
    {
        var submissionId = Guid.NewGuid();
        await projector.ProjectAsync(
            PersonalQuoteMessage(Guid.NewGuid(), submissionId, Guid.NewGuid(), 1),
            TestContext.Current.CancellationToken);
        await projector.ProjectAsync(
            PersonalQuoteMessage(Guid.NewGuid(), submissionId, Guid.NewGuid(), 2),
            TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var entries = await dbContext.NotificationInboxEntries
            .OrderBy(entry => entry.OccurredAtUtc)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, entries.Count);
        Assert.Equal(NotificationLifecycleState.Historical, entries[0].LifecycleState);
        Assert.Null(entries[0].ReadAtUtc);
        Assert.Equal(2, entries[0].ReplacementQuoteVersion);
        Assert.Equal(NotificationLifecycleState.Active, entries[1].LifecycleState);

        var personalRepository = new EfNotificationInboxRepository(dbContext);
        Assert.Equal(1, await personalRepository.CountUnreadForRecipientAsync(
            "cust-1",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delayed_Team_Projection_Uses_Subject_View_Watermark()
    {
        var subjectId = Guid.NewGuid().ToString();
        var acknowledgements = new EfNotificationSubjectAcknowledgementRepository(dbContext);
        await acknowledgements.AcknowledgeAsync(
            "uw-a",
            NotificationScopes.Team,
            [NotificationAudiences.UnderwritingOperations],
            "evidence-request",
            subjectId,
            new DateTime(2026, 7, 16, 10, 0, 0, DateTimeKind.Utc),
            TestContext.Current.CancellationToken);

        var message = TeamMessage(Guid.NewGuid()) with
        {
            Type = NotificationMessageTypes.EvidenceRequestResponded,
            SubjectReferenceType = "evidence-request",
            SubjectReferenceId = subjectId,
            OccurredAtUtc = new DateTime(2026, 7, 16, 9, 59, 0, DateTimeKind.Utc)
        };
        await projector.ProjectAsync(message, TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        Assert.True(Assert.Single(await repository.ListForAudiencesAsync(
            "uw-a",
            [NotificationAudiences.UnderwritingOperations],
            TestContext.Current.CancellationToken)).IsRead);
        Assert.False(Assert.Single(await repository.ListForAudiencesAsync(
            "uw-b",
            [NotificationAudiences.UnderwritingOperations],
            TestContext.Current.CancellationToken)).IsRead);
    }

    private static NotificationMessage TeamMessage(Guid outboxMessageId) => new(
        outboxMessageId.ToString("N"),
        outboxMessageId,
        NotificationMessageTypes.QuoteReferredForUnderwriting,
        NotificationAudiences.UnderwritingOperations,
        OwnerUserId: string.Empty,
        SubjectReferenceType: "quote",
        SubjectReferenceId: Guid.NewGuid().ToString(),
        OccurredAtUtc: new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc),
        Attributes: new Dictionary<string, string>());

    private static NotificationMessage PersonalQuoteMessage(
        Guid outboxMessageId,
        Guid submissionId,
        Guid quoteId,
        int quoteVersion) => new(
        outboxMessageId.ToString("N"),
        outboxMessageId,
        NotificationMessageTypes.QuoteReady,
        NotificationAudiences.CustomerOrBroker,
        OwnerUserId: "cust-1",
        SubjectReferenceType: "quote",
        SubjectReferenceId: quoteId.ToString(),
        OccurredAtUtc: new DateTime(2026, 7, 12, quoteVersion, 0, 0, DateTimeKind.Utc),
        Attributes: new Dictionary<string, string>
        {
            ["submissionId"] = submissionId.ToString(),
            ["quoteId"] = quoteId.ToString(),
            ["version"] = quoteVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });

    public void Dispose()
    {
        dbContext.Dispose();
        connection.Dispose();
    }
}
