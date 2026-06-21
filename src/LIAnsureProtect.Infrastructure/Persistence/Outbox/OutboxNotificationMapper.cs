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
