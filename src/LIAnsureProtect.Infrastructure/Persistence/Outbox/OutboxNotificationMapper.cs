using System.Text.Json;
using LIAnsureProtect.Application.Notifications;
using LIAnsureProtect.Domain.Policies;
using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

internal static class OutboxNotificationMapper
{
    public static NotificationMessage? TryMap(OutboxMessage outboxMessage)
    {
        return outboxMessage.Type switch
        {
            nameof(QuoteGeneratedDomainEvent) => MapQuoteGenerated(outboxMessage),
            nameof(QuoteUnderwritingDecisionRecordedDomainEvent) => MapUnderwritingDecision(outboxMessage),
            nameof(QuoteAcceptedDomainEvent) => MapQuoteAccepted(outboxMessage),
            nameof(PolicyBoundDomainEvent) => MapPolicyBound(outboxMessage),
            nameof(QuoteEvidenceRequestCreatedDomainEvent) => MapEvidenceRequestCreated(outboxMessage),
            nameof(QuoteEvidenceRequestRespondedDomainEvent) => MapEvidenceRequestResponded(outboxMessage),
            nameof(QuoteEvidenceRequestAcceptedDomainEvent) => MapEvidenceRequestAccepted(outboxMessage),
            nameof(QuoteEvidenceRequestCancelledDomainEvent) => MapEvidenceRequestCancelled(outboxMessage),
            nameof(QuoteEvidenceRequestFollowUpSentDomainEvent) => MapEvidenceRequestFollowUpSent(outboxMessage),
            nameof(QuoteEvidenceRequestRemediationRequiredDomainEvent) => MapEvidenceRequestRemediationRequired(outboxMessage),
            _ => null
        };
    }

    private static NotificationMessage MapQuoteGenerated(OutboxMessage outboxMessage)
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

    private static NotificationMessage MapUnderwritingDecision(OutboxMessage outboxMessage)
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

    private static NotificationMessage MapQuoteAccepted(OutboxMessage outboxMessage)
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

    private static NotificationMessage MapPolicyBound(OutboxMessage outboxMessage)
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

    private static NotificationMessage MapEvidenceRequestCreated(OutboxMessage outboxMessage)
    {
        var domainEvent = Deserialize<QuoteEvidenceRequestCreatedDomainEvent>(outboxMessage);

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

    private static NotificationMessage MapEvidenceRequestResponded(OutboxMessage outboxMessage)
    {
        var domainEvent = Deserialize<QuoteEvidenceRequestRespondedDomainEvent>(outboxMessage);

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

    private static NotificationMessage MapEvidenceRequestAccepted(OutboxMessage outboxMessage)
    {
        var domainEvent = Deserialize<QuoteEvidenceRequestAcceptedDomainEvent>(outboxMessage);

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

    private static NotificationMessage MapEvidenceRequestCancelled(OutboxMessage outboxMessage)
    {
        var domainEvent = Deserialize<QuoteEvidenceRequestCancelledDomainEvent>(outboxMessage);

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

    private static NotificationMessage MapEvidenceRequestFollowUpSent(OutboxMessage outboxMessage)
    {
        var domainEvent = Deserialize<QuoteEvidenceRequestFollowUpSentDomainEvent>(outboxMessage);

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

    private static NotificationMessage MapEvidenceRequestRemediationRequired(OutboxMessage outboxMessage)
    {
        var domainEvent = Deserialize<QuoteEvidenceRequestRemediationRequiredDomainEvent>(outboxMessage);

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
        OutboxMessage outboxMessage,
        string type,
        string audience,
        string ownerUserId,
        Guid evidenceRequestId,
        Guid quoteId,
        Guid submissionId,
        string requestedByUserId,
        EvidenceRequestCategory category,
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
                NotificationMessageTypes.EvidenceRequestResponded => EvidenceRequestStatus.Responded.ToString(),
                NotificationMessageTypes.EvidenceRequestAccepted => EvidenceRequestStatus.Accepted.ToString(),
                NotificationMessageTypes.EvidenceRequestCancelled => EvidenceRequestStatus.Cancelled.ToString(),
                NotificationMessageTypes.EvidenceRequestRemediationRequired => EvidenceRequestStatus.Responded.ToString(),
                _ => EvidenceRequestStatus.Open.ToString()
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
        OutboxMessage outboxMessage,
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

    private static T Deserialize<T>(OutboxMessage outboxMessage)
    {
        return JsonSerializer.Deserialize<T>(outboxMessage.Payload)
            ?? throw new InvalidOperationException($"Outbox message {outboxMessage.Id} payload could not be deserialized.");
    }
}
