using System.Globalization;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using ModuleClaimAcceptedDomainEvent = LIAnsureProtect.Modules.Claims.Domain.ClaimAcceptedDomainEvent;
using ModuleClaimAssignedDomainEvent = LIAnsureProtect.Modules.Claims.Domain.ClaimAssignedDomainEvent;
using ModuleClaimClosedDomainEvent = LIAnsureProtect.Modules.Claims.Domain.ClaimClosedDomainEvent;
using ModuleClaimDeniedDomainEvent = LIAnsureProtect.Modules.Claims.Domain.ClaimDeniedDomainEvent;
using ModuleClaimFiledDomainEvent = LIAnsureProtect.Modules.Claims.Domain.ClaimFiledDomainEvent;
using ModuleClaimInformationRequestedDomainEvent = LIAnsureProtect.Modules.Claims.Domain.ClaimInformationRequestedDomainEvent;
using ModuleClaimantInformationResponseDomainEvent = LIAnsureProtect.Modules.Claims.Domain.ClaimantInformationResponseDomainEvent;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.Notifications;

/// <summary>
/// CM6: registered mappers turning the Claims module's outbox events into notification messages —
/// personal (customer-or-broker) for everything that affects the claimant, and the
/// claims-operations team inbox for what the department works from. Deserializing the module's
/// event types here is the same named transitional dispatcher seam as the Underwriting events.
/// </summary>
public sealed class ClaimFiledNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => "ClaimFiledDomainEvent";

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleClaimFiledDomainEvent>(outboxMessage);

        return ClaimNotificationMessageFactory.Create(
            outboxMessage,
            NotificationMessageTypes.ClaimFiled,
            NotificationAudiences.ClaimsOperations,
            domainEvent.OwnerUserId,
            domainEvent.ClaimId,
            domainEvent.ClaimNumber,
            domainEvent.PolicyId,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["policyNumber"] = domainEvent.PolicyNumber,
                ["incidentType"] = domainEvent.IncidentType.ToString()
            });
    }
}

public sealed class ClaimAssignedNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => "ClaimAssignedDomainEvent";

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleClaimAssignedDomainEvent>(outboxMessage);

        return ClaimNotificationMessageFactory.Create(
            outboxMessage,
            NotificationMessageTypes.ClaimAssigned,
            NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            domainEvent.ClaimId,
            domainEvent.ClaimNumber,
            domainEvent.PolicyId,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["adjusterUserId"] = domainEvent.AdjusterUserId
            });
    }
}

public sealed class ClaimInformationRequestedNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => "ClaimInformationRequestedDomainEvent";

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleClaimInformationRequestedDomainEvent>(outboxMessage);

        // Remediation-style: the claimant must act for their claim to move forward.
        return ClaimNotificationMessageFactory.Create(
            outboxMessage,
            NotificationMessageTypes.ClaimInformationRequested,
            NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            domainEvent.ClaimId,
            domainEvent.ClaimNumber,
            domainEvent.PolicyId,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["informationRequestId"] = domainEvent.InformationRequestId.ToString(),
                ["title"] = domainEvent.Title,
                ["requestedByUserId"] = domainEvent.RequestedByUserId,
                ["actionRequired"] = "true"
            });
    }
}

public sealed class ClaimantInformationResponseNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => "ClaimantInformationResponseDomainEvent";

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleClaimantInformationResponseDomainEvent>(outboxMessage);

        var attributes = new Dictionary<string, string>
        {
            ["informationRequestId"] = domainEvent.InformationRequestId.ToString(),
            ["respondedByUserId"] = domainEvent.RespondedByUserId
        };
        if (!string.IsNullOrWhiteSpace(domainEvent.AssignedAdjusterUserId))
            attributes["assignedAdjusterUserId"] = domainEvent.AssignedAdjusterUserId;

        return ClaimNotificationMessageFactory.Create(
            outboxMessage,
            NotificationMessageTypes.ClaimInformationResponse,
            NotificationAudiences.ClaimsOperations,
            domainEvent.OwnerUserId,
            domainEvent.ClaimId,
            domainEvent.ClaimNumber,
            domainEvent.PolicyId,
            domainEvent.OccurredAtUtc,
            attributes);
    }
}

public sealed class ClaimAcceptedNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => "ClaimAcceptedDomainEvent";

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleClaimAcceptedDomainEvent>(outboxMessage);

        return ClaimNotificationMessageFactory.Create(
            outboxMessage,
            NotificationMessageTypes.ClaimAccepted,
            NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            domainEvent.ClaimId,
            domainEvent.ClaimNumber,
            domainEvent.PolicyId,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["settlementAmount"] = domainEvent.SettlementAmount.ToString("0.00", CultureInfo.InvariantCulture),
                ["decidedByUserId"] = domainEvent.DecidedByUserId
            });
    }
}

public sealed class ClaimDeniedNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => "ClaimDeniedDomainEvent";

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleClaimDeniedDomainEvent>(outboxMessage);

        return ClaimNotificationMessageFactory.Create(
            outboxMessage,
            NotificationMessageTypes.ClaimDenied,
            NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            domainEvent.ClaimId,
            domainEvent.ClaimNumber,
            domainEvent.PolicyId,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["denialReason"] = domainEvent.DenialReason.ToString(),
                ["decidedByUserId"] = domainEvent.DecidedByUserId
            });
    }
}

public sealed class ClaimClosedNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => "ClaimClosedDomainEvent";

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleClaimClosedDomainEvent>(outboxMessage);

        return ClaimNotificationMessageFactory.Create(
            outboxMessage,
            NotificationMessageTypes.ClaimClosed,
            NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            domainEvent.ClaimId,
            domainEvent.ClaimNumber,
            domainEvent.PolicyId,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["outcomeAtClose"] = domainEvent.OutcomeAtClose.ToString(),
                ["closedByUserId"] = domainEvent.ClosedByUserId
            });
    }
}

internal static class ClaimNotificationMessageFactory
{
    public static NotificationMessage Create(
        IOutboxMessageView outboxMessage,
        string type,
        string audience,
        string ownerUserId,
        Guid claimId,
        string claimNumber,
        Guid policyId,
        DateTime occurredAtUtc,
        Dictionary<string, string> extraAttributes)
    {
        var attributes = new Dictionary<string, string>
        {
            ["claimNumber"] = claimNumber,
            ["policyId"] = policyId.ToString()
        };

        foreach (var attribute in extraAttributes)
        {
            attributes[attribute.Key] = attribute.Value;
        }

        return NotificationMessageFactory.CreateMessage(
            outboxMessage,
            type,
            audience,
            ownerUserId,
            "claim",
            claimId,
            occurredAtUtc,
            attributes);
    }
}
