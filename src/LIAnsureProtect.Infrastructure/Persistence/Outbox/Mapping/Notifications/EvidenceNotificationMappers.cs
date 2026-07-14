using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using ModuleQuoteEvidenceRequestAcceptedDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestAcceptedDomainEvent;
using ModuleQuoteEvidenceRequestCancelledDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestCancelledDomainEvent;
using ModuleQuoteEvidenceRequestCreatedDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestCreatedDomainEvent;
using ModuleQuoteEvidenceRequestFollowUpSentDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestFollowUpSentDomainEvent;
using ModuleQuoteEvidenceRequestRemediationRequiredDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestRemediationRequiredDomainEvent;
using ModuleQuoteEvidenceRequestRespondedDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestRespondedDomainEvent;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.Notifications;

public sealed class EvidenceRequestCreatedNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => "QuoteEvidenceRequestCreatedDomainEvent";

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleQuoteEvidenceRequestCreatedDomainEvent>(outboxMessage);

        return NotificationMessageFactory.CreateEvidenceMessage(
            outboxMessage,
            NotificationMessageTypes.EvidenceRequestCreated,
            NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            domainEvent.EvidenceRequestId,
            domainEvent.QuoteId,
            domainEvent.SubmissionId,
            domainEvent.RequestedByUserId,
            domainEvent.Category,
            domainEvent.DueAtUtc,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["requestTitle"] = domainEvent.Title ?? domainEvent.Category.ToString(),
                ["quoteVersion"] = domainEvent.QuoteVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            domainEvent.SubmissionReference,
            domainEvent.CompanyName);
    }
}

public sealed class EvidenceRequestRespondedNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => "QuoteEvidenceRequestRespondedDomainEvent";

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleQuoteEvidenceRequestRespondedDomainEvent>(outboxMessage);

        return NotificationMessageFactory.CreateEvidenceMessage(
            outboxMessage,
            NotificationMessageTypes.EvidenceRequestResponded,
            NotificationAudiences.UnderwritingOperations,
            domainEvent.OwnerUserId,
            domainEvent.EvidenceRequestId,
            domainEvent.QuoteId,
            domainEvent.SubmissionId,
            domainEvent.RequestedByUserId,
            domainEvent.Category,
            domainEvent.DueAtUtc,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["respondedByUserId"] = domainEvent.RespondedByUserId
            },
            domainEvent.SubmissionReference,
            domainEvent.CompanyName);
    }
}

public sealed class EvidenceRequestAcceptedNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => "QuoteEvidenceRequestAcceptedDomainEvent";

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleQuoteEvidenceRequestAcceptedDomainEvent>(outboxMessage);

        return NotificationMessageFactory.CreateEvidenceMessage(
            outboxMessage,
            NotificationMessageTypes.EvidenceRequestAccepted,
            NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            domainEvent.EvidenceRequestId,
            domainEvent.QuoteId,
            domainEvent.SubmissionId,
            domainEvent.RequestedByUserId,
            domainEvent.Category,
            domainEvent.DueAtUtc,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["acceptedByUserId"] = domainEvent.AcceptedByUserId
            },
            domainEvent.SubmissionReference,
            domainEvent.CompanyName);
    }
}

public sealed class EvidenceRequestCancelledNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => "QuoteEvidenceRequestCancelledDomainEvent";

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleQuoteEvidenceRequestCancelledDomainEvent>(outboxMessage);

        return NotificationMessageFactory.CreateEvidenceMessage(
            outboxMessage,
            NotificationMessageTypes.EvidenceRequestCancelled,
            NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            domainEvent.EvidenceRequestId,
            domainEvent.QuoteId,
            domainEvent.SubmissionId,
            domainEvent.RequestedByUserId,
            domainEvent.Category,
            domainEvent.DueAtUtc,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["cancelledByUserId"] = domainEvent.CancelledByUserId
            },
            domainEvent.SubmissionReference,
            domainEvent.CompanyName);
    }
}

public sealed class EvidenceRequestFollowUpSentNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => "QuoteEvidenceRequestFollowUpSentDomainEvent";

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleQuoteEvidenceRequestFollowUpSentDomainEvent>(outboxMessage);

        return NotificationMessageFactory.CreateEvidenceMessage(
            outboxMessage,
            NotificationMessageTypes.EvidenceRequestFollowUpSent,
            NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            domainEvent.EvidenceRequestId,
            domainEvent.QuoteId,
            domainEvent.SubmissionId,
            domainEvent.RequestedByUserId,
            domainEvent.Category,
            domainEvent.DueAtUtc,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["followedUpByUserId"] = domainEvent.FollowedUpByUserId
            },
            domainEvent.SubmissionReference,
            domainEvent.CompanyName);
    }
}

public sealed class EvidenceRequestRemediationRequiredNotificationMapper
    : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => "QuoteEvidenceRequestRemediationRequiredDomainEvent";

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleQuoteEvidenceRequestRemediationRequiredDomainEvent>(
            outboxMessage);

        return NotificationMessageFactory.CreateEvidenceMessage(
            outboxMessage,
            NotificationMessageTypes.EvidenceRequestRemediationRequired,
            NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            domainEvent.EvidenceRequestId,
            domainEvent.QuoteId,
            domainEvent.SubmissionId,
            domainEvent.RequestedByUserId,
            domainEvent.Category,
            domainEvent.DueAtUtc,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["reviewedByUserId"] = domainEvent.ReviewedByUserId,
                ["decision"] = domainEvent.Decision.ToString(),
                ["reviewReason"] = domainEvent.ReviewReason,
                ["remediationGuidance"] = domainEvent.RemediationGuidance,
                ["actionRequired"] = "true"
            },
            domainEvent.SubmissionReference,
            domainEvent.CompanyName);
    }
}
