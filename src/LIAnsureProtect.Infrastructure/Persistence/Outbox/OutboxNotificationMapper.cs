using System.Text.Json;
using LIAnsureProtect.Domain.Policies;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using ModuleEvidenceRequestCategory = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.EvidenceRequestCategory;
using ModuleEvidenceRequestStatus = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.EvidenceRequestStatus;
using ModuleQuoteEvidenceRequestAcceptedDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestAcceptedDomainEvent;
using ModuleQuoteEvidenceRequestCancelledDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestCancelledDomainEvent;
using ModuleQuoteEvidenceRequestCreatedDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestCreatedDomainEvent;
using ModuleQuoteEvidenceRequestFollowUpSentDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestFollowUpSentDomainEvent;
using ModuleQuoteEvidenceRequestRemediationRequiredDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestRemediationRequiredDomainEvent;
using ModuleQuoteEvidenceRequestRespondedDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestRespondedDomainEvent;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

internal static class OutboxNotificationMapper
{
    private const string QuoteEvidenceRequestCreatedDomainEventType = "QuoteEvidenceRequestCreatedDomainEvent";
    private const string QuoteEvidenceRequestRespondedDomainEventType = "QuoteEvidenceRequestRespondedDomainEvent";
    private const string QuoteEvidenceRequestAcceptedDomainEventType = "QuoteEvidenceRequestAcceptedDomainEvent";
    private const string QuoteEvidenceRequestCancelledDomainEventType = "QuoteEvidenceRequestCancelledDomainEvent";
    private const string QuoteEvidenceRequestFollowUpSentDomainEventType = "QuoteEvidenceRequestFollowUpSentDomainEvent";
    private const string QuoteEvidenceRequestRemediationRequiredDomainEventType = "QuoteEvidenceRequestRemediationRequiredDomainEvent";

    public static NotificationMessage? TryMap(IOutboxMessageView outboxMessage)
    {
        return outboxMessage.Type switch
        {
            nameof(QuoteGeneratedDomainEvent) => MapQuoteGenerated(outboxMessage),
            nameof(QuoteUnderwritingDecisionRecordedDomainEvent) => MapUnderwritingDecision(outboxMessage),
            nameof(QuoteAcceptedDomainEvent) => MapQuoteAccepted(outboxMessage),
            nameof(PolicyBoundDomainEvent) => MapPolicyBound(outboxMessage),
            QuoteEvidenceRequestCreatedDomainEventType => MapEvidenceRequestCreated(outboxMessage),
            QuoteEvidenceRequestRespondedDomainEventType => MapEvidenceRequestResponded(outboxMessage),
            QuoteEvidenceRequestAcceptedDomainEventType => MapEvidenceRequestAccepted(outboxMessage),
            QuoteEvidenceRequestCancelledDomainEventType => MapEvidenceRequestCancelled(outboxMessage),
            QuoteEvidenceRequestFollowUpSentDomainEventType => MapEvidenceRequestFollowUpSent(outboxMessage),
            QuoteEvidenceRequestRemediationRequiredDomainEventType => MapEvidenceRequestRemediationRequired(outboxMessage),
            _ => null
        };
    }

    private static NotificationMessage MapQuoteGenerated(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<QuoteGeneratedDomainEvent>(outboxMessage);
        var isReferred = domainEvent.Status == QuoteStatus.Referred;

        return CreateMessage(
            outboxMessage,
            isReferred
                ? NotificationMessageTypes.QuoteReferredForUnderwriting
                : NotificationMessageTypes.QuoteReady,
            isReferred
                ? NotificationAudiences.UnderwritingOperations
                : NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            "quote",
            domainEvent.QuoteId,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["quoteId"] = domainEvent.QuoteId.ToString(),
                ["submissionId"] = domainEvent.SubmissionId.ToString(),
                ["status"] = domainEvent.Status.ToString()
            });
    }

    private static NotificationMessage MapUnderwritingDecision(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<QuoteUnderwritingDecisionRecordedDomainEvent>(outboxMessage);

        return CreateMessage(
            outboxMessage,
            NotificationMessageTypes.QuoteUnderwritingDecisionRecorded,
            NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            "quote",
            domainEvent.QuoteId,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["quoteId"] = domainEvent.QuoteId.ToString(),
                ["submissionId"] = domainEvent.SubmissionId.ToString(),
                ["reviewedByUserId"] = domainEvent.ReviewedByUserId,
                ["decision"] = domainEvent.Decision.ToString()
            });
    }

    private static NotificationMessage MapQuoteAccepted(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<QuoteAcceptedDomainEvent>(outboxMessage);

        return CreateMessage(
            outboxMessage,
            NotificationMessageTypes.QuoteAccepted,
            NotificationAudiences.BindingOperations,
            domainEvent.OwnerUserId,
            "quote",
            domainEvent.QuoteId,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["quoteId"] = domainEvent.QuoteId.ToString(),
                ["submissionId"] = domainEvent.SubmissionId.ToString(),
                ["acceptedByUserId"] = domainEvent.AcceptedByUserId
            });
    }

    private static NotificationMessage MapPolicyBound(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<PolicyBoundDomainEvent>(outboxMessage);

        return CreateMessage(
            outboxMessage,
            NotificationMessageTypes.PolicyBound,
            NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            "policy",
            domainEvent.PolicyId,
            domainEvent.OccurredAtUtc,
            new Dictionary<string, string>
            {
                ["policyId"] = domainEvent.PolicyId.ToString(),
                ["policyNumber"] = domainEvent.PolicyNumber,
                ["quoteId"] = domainEvent.QuoteId.ToString(),
                ["submissionId"] = domainEvent.SubmissionId.ToString(),
                ["boundByUserId"] = domainEvent.BoundByUserId
            });
    }

    private static NotificationMessage MapEvidenceRequestCreated(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<ModuleQuoteEvidenceRequestCreatedDomainEvent>(outboxMessage);

        return CreateEvidenceMessage(
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
            new Dictionary<string, string>());
    }

    private static NotificationMessage MapEvidenceRequestResponded(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<ModuleQuoteEvidenceRequestRespondedDomainEvent>(outboxMessage);

        return CreateEvidenceMessage(
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
            });
    }

    private static NotificationMessage MapEvidenceRequestAccepted(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<ModuleQuoteEvidenceRequestAcceptedDomainEvent>(outboxMessage);

        return CreateEvidenceMessage(
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
            });
    }

    private static NotificationMessage MapEvidenceRequestCancelled(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<ModuleQuoteEvidenceRequestCancelledDomainEvent>(outboxMessage);

        return CreateEvidenceMessage(
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
            });
    }

    private static NotificationMessage MapEvidenceRequestFollowUpSent(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<ModuleQuoteEvidenceRequestFollowUpSentDomainEvent>(outboxMessage);

        return CreateEvidenceMessage(
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
            });
    }

    private static NotificationMessage MapEvidenceRequestRemediationRequired(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<ModuleQuoteEvidenceRequestRemediationRequiredDomainEvent>(outboxMessage);

        return CreateEvidenceMessage(
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
            });
    }

    private static NotificationMessage CreateEvidenceMessage(
        IOutboxMessageView outboxMessage,
        string type,
        string audience,
        string ownerUserId,
        Guid evidenceRequestId,
        Guid quoteId,
        Guid submissionId,
        string requestedByUserId,
        ModuleEvidenceRequestCategory category,
        DateTime dueAtUtc,
        DateTime occurredAtUtc,
        Dictionary<string, string> extraAttributes)
    {
        var attributes = new Dictionary<string, string>
        {
            ["evidenceRequestId"] = evidenceRequestId.ToString(),
            ["quoteId"] = quoteId.ToString(),
            ["submissionId"] = submissionId.ToString(),
            ["requestedByUserId"] = requestedByUserId,
            ["category"] = category.ToString(),
            ["status"] = type switch
            {
                NotificationMessageTypes.EvidenceRequestResponded => ModuleEvidenceRequestStatus.Responded.ToString(),
                NotificationMessageTypes.EvidenceRequestAccepted => ModuleEvidenceRequestStatus.Accepted.ToString(),
                NotificationMessageTypes.EvidenceRequestCancelled => ModuleEvidenceRequestStatus.Cancelled.ToString(),
                NotificationMessageTypes.EvidenceRequestRemediationRequired => ModuleEvidenceRequestStatus.Responded.ToString(),
                _ => ModuleEvidenceRequestStatus.Open.ToString()
            },
            ["dueAtUtc"] = dueAtUtc.ToString("O")
        };

        foreach (var attribute in extraAttributes)
        {
            attributes[attribute.Key] = attribute.Value;
        }

        return CreateMessage(
            outboxMessage,
            type,
            audience,
            ownerUserId,
            "evidence-request",
            evidenceRequestId,
            occurredAtUtc,
            attributes);
    }

    private static NotificationMessage CreateMessage(
        IOutboxMessageView outboxMessage,
        string type,
        string audience,
        string ownerUserId,
        string subjectReferenceType,
        Guid subjectReferenceId,
        DateTime occurredAtUtc,
        IReadOnlyDictionary<string, string> attributes)
    {
        return new NotificationMessage(
            outboxMessage.Id.ToString("N"),
            outboxMessage.Id,
            type,
            audience,
            ownerUserId,
            subjectReferenceType,
            subjectReferenceId.ToString(),
            occurredAtUtc,
            attributes);
    }

    private static T Deserialize<T>(IOutboxMessageView outboxMessage)
    {
        return JsonSerializer.Deserialize<T>(outboxMessage.Payload)
            ?? throw new InvalidOperationException($"Outbox message {outboxMessage.Id} payload could not be deserialized.");
    }
}
