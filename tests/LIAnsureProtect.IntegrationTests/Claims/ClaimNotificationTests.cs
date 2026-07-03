using System.Text.Json;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.Notifications;
using LIAnsureProtect.Modules.Claims.Domain;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;
using LIAnsureProtect.Platform.Abstractions.DomainEvents;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.IntegrationTests.Claims;

/// <summary>
/// CM6: the claim events map into notification messages (personal for the claimant, the new
/// claims-operations team audience for the department), the projector persists the new team
/// audience, and the role→audience matrix gives ClaimsAdjuster its inbox.
/// </summary>
public sealed class ClaimNotificationTests
{
    private static readonly Guid ClaimId = Guid.Parse("c1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid PolicyId = Guid.Parse("c1a1a1a1-0000-4000-8000-000000000002");
    private static readonly DateTime OccurredAtUtc = new(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);

    private static FakeOutboxMessageView View(IDomainEvent domainEvent)
        => new(Guid.NewGuid(), domainEvent.GetType().Name, JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

    // --- Mappers ---

    [Fact]
    public void ClaimFiled_Maps_To_The_Claims_Operations_Team()
    {
        var mapper = new ClaimFiledNotificationMapper();
        var message = mapper.Map(View(new ClaimFiledDomainEvent(
            ClaimId, "CLM-CYB-20260401-AAAAAAAA", PolicyId, "LIP-CYB-20260101-BBBBBBBB",
            "customer-1", ClaimIncidentType.RansomwareExtortion, OccurredAtUtc)));

        Assert.Equal(NotificationMessageTypes.ClaimFiled, message.Type);
        Assert.Equal(NotificationAudiences.ClaimsOperations, message.Audience);
        Assert.Equal("claim", message.SubjectReferenceType);
        Assert.Equal(ClaimId.ToString(), message.SubjectReferenceId);
        Assert.Equal("CLM-CYB-20260401-AAAAAAAA", message.Attributes["claimNumber"]);
        Assert.Equal("RansomwareExtortion", message.Attributes["incidentType"]);
    }

    [Fact]
    public void ClaimAssigned_Notifies_The_Claimant()
    {
        var mapper = new ClaimAssignedNotificationMapper();
        var message = mapper.Map(View(new ClaimAssignedDomainEvent(
            ClaimId, "CLM-CYB-20260401-AAAAAAAA", PolicyId, "customer-1", "adjuster-1", OccurredAtUtc)));

        Assert.Equal(NotificationMessageTypes.ClaimAssigned, message.Type);
        Assert.Equal(NotificationAudiences.CustomerOrBroker, message.Audience);
        Assert.Equal("customer-1", message.OwnerUserId);
        Assert.Equal("adjuster-1", message.Attributes["adjusterUserId"]);
    }

    [Fact]
    public void InformationRequest_Is_A_Remediation_Style_Claimant_Message()
    {
        var requestId = Guid.NewGuid();
        var mapper = new ClaimInformationRequestedNotificationMapper();
        var message = mapper.Map(View(new ClaimInformationRequestedDomainEvent(
            ClaimId, "CLM-CYB-20260401-AAAAAAAA", requestId, PolicyId,
            "customer-1", "adjuster-1", "Proof of loss", OccurredAtUtc)));

        Assert.Equal(NotificationMessageTypes.ClaimInformationRequested, message.Type);
        Assert.Equal(NotificationAudiences.CustomerOrBroker, message.Audience);
        Assert.Equal("true", message.Attributes["actionRequired"]);
        Assert.Equal("Proof of loss", message.Attributes["title"]);
        Assert.Equal(requestId.ToString(), message.Attributes["informationRequestId"]);
    }

    [Fact]
    public void Claimant_Response_Reaches_The_Claims_Operations_Team()
    {
        var mapper = new ClaimantInformationResponseNotificationMapper();
        var message = mapper.Map(View(new ClaimantInformationResponseDomainEvent(
            ClaimId, "CLM-CYB-20260401-AAAAAAAA", Guid.NewGuid(), PolicyId,
            "customer-1", "customer-1", "adjuster-1", OccurredAtUtc)));

        Assert.Equal(NotificationMessageTypes.ClaimInformationResponse, message.Type);
        Assert.Equal(NotificationAudiences.ClaimsOperations, message.Audience);
        Assert.Equal("adjuster-1", message.Attributes["assignedAdjusterUserId"]);
    }

    [Fact]
    public void Accept_Notifies_The_Claimant_With_The_Settlement()
    {
        var mapper = new ClaimAcceptedNotificationMapper();
        var message = mapper.Map(View(new ClaimAcceptedDomainEvent(
            ClaimId, "CLM-CYB-20260401-AAAAAAAA", PolicyId, "customer-1", "adjuster-1", 300_000m, OccurredAtUtc)));

        Assert.Equal(NotificationMessageTypes.ClaimAccepted, message.Type);
        Assert.Equal(NotificationAudiences.CustomerOrBroker, message.Audience);
        Assert.Equal("300000.00", message.Attributes["settlementAmount"]);
    }

    [Fact]
    public void Deny_Notifies_The_Claimant_With_The_Reason()
    {
        var mapper = new ClaimDeniedNotificationMapper();
        var message = mapper.Map(View(new ClaimDeniedDomainEvent(
            ClaimId, "CLM-CYB-20260401-AAAAAAAA", PolicyId, "customer-1", "adjuster-1",
            ClaimDenialReason.PolicyExclusion, OccurredAtUtc)));

        Assert.Equal(NotificationMessageTypes.ClaimDenied, message.Type);
        Assert.Equal(NotificationAudiences.CustomerOrBroker, message.Audience);
        Assert.Equal("PolicyExclusion", message.Attributes["denialReason"]);
    }

    [Fact]
    public void Close_Notifies_The_Claimant_With_The_Outcome()
    {
        var mapper = new ClaimClosedNotificationMapper();
        var message = mapper.Map(View(new ClaimClosedDomainEvent(
            ClaimId, "CLM-CYB-20260401-AAAAAAAA", PolicyId, "customer-1", "adjuster-1",
            ClaimStatus.Accepted, OccurredAtUtc)));

        Assert.Equal(NotificationMessageTypes.ClaimClosed, message.Type);
        Assert.Equal(NotificationAudiences.CustomerOrBroker, message.Audience);
        Assert.Equal("Accepted", message.Attributes["outcomeAtClose"]);
    }

    // --- Role → team-audience matrix ---

    [Fact]
    public void ClaimsAdjuster_Sees_Only_The_Claims_Operations_Inbox()
    {
        var audiences = NotificationTeamAudiences.ForRoles(["ClaimsAdjuster"]);

        Assert.Equal([NotificationAudiences.ClaimsOperations], audiences);
    }

    [Fact]
    public void Underwriter_Audiences_Are_Unchanged()
    {
        var audiences = NotificationTeamAudiences.ForRoles(["Underwriter"]);

        Assert.Contains(NotificationAudiences.UnderwritingOperations, audiences);
        Assert.Contains(NotificationAudiences.BindingOperations, audiences);
        Assert.DoesNotContain(NotificationAudiences.ClaimsOperations, audiences);
    }

    [Fact]
    public void Admin_Sees_All_Team_Inboxes()
    {
        var audiences = NotificationTeamAudiences.ForRoles(["Admin"]);

        Assert.Contains(NotificationAudiences.UnderwritingOperations, audiences);
        Assert.Contains(NotificationAudiences.BindingOperations, audiences);
        Assert.Contains(NotificationAudiences.ClaimsOperations, audiences);
    }

    [Fact]
    public void Customers_See_No_Team_Inboxes()
    {
        Assert.Empty(NotificationTeamAudiences.ForRoles(["Customer", "Broker"]));
    }

    [Fact]
    public void Combined_Roles_Union_Their_Audiences()
    {
        var audiences = NotificationTeamAudiences.ForRoles(["Underwriter", "ClaimsAdjuster"]);

        Assert.Contains(NotificationAudiences.UnderwritingOperations, audiences);
        Assert.Contains(NotificationAudiences.ClaimsOperations, audiences);
    }

    // --- Projector: the new team audience persists a shared entry ---

    [Fact]
    public async Task Projector_Persists_A_Shared_ClaimsOperations_Entry_Idempotently()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var dbContext = new NotificationsDbContext(
            new DbContextOptionsBuilder<NotificationsDbContext>().UseSqlite(connection).Options);
        dbContext.Database.EnsureCreated();
        var projector = new NotificationInboxProjector(dbContext);
        var outboxMessageId = Guid.NewGuid();
        var message = new NotificationMessage(
            outboxMessageId.ToString("N"),
            outboxMessageId,
            NotificationMessageTypes.ClaimFiled,
            NotificationAudiences.ClaimsOperations,
            "customer-1",
            "claim",
            ClaimId.ToString(),
            OccurredAtUtc,
            new Dictionary<string, string> { ["claimNumber"] = "CLM-CYB-20260401-AAAAAAAA" });

        await projector.ProjectAsync(message, TestContext.Current.CancellationToken);
        await projector.ProjectAsync(message, TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var entry = await dbContext.TeamNotificationEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(NotificationAudiences.ClaimsOperations, entry.Audience);
        Assert.Equal(outboxMessageId, entry.SourceOutboxMessageId);
        Assert.Empty(await dbContext.NotificationInboxEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    private sealed class FakeOutboxMessageView(Guid id, string type, string payload) : IOutboxMessageView
    {
        public Guid Id { get; } = id;

        public string Type { get; } = type;

        public string Payload { get; } = payload;

        public DateTime CreatedAtUtc => new(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);

        public int PublishAttemptCount => 0;

        public void MarkProcessed(DateTime processedAtUtc)
        {
        }

        public void MarkPublishSucceeded(DateTime processedAtUtc, string providerMessageId)
        {
        }

        public void MarkPublishFailed(DateTime attemptedAtUtc, string failureReason, DateTime? nextAttemptAtUtc, bool exhausted)
        {
        }
    }
}
