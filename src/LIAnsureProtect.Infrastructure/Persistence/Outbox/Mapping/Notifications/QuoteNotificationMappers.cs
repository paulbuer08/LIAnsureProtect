using LIAnsureProtect.Domain.Policies;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.Notifications;

public sealed class QuoteGeneratedNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => nameof(QuoteGeneratedDomainEvent);

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<QuoteGeneratedDomainEvent>(outboxMessage);
        var isReferred = domainEvent.Status == QuoteStatus.Referred;

        return NotificationMessageFactory.CreateMessage(
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
            AddSubmissionContext(new Dictionary<string, string>
            {
                ["quoteId"] = domainEvent.QuoteId.ToString(),
                ["submissionId"] = domainEvent.SubmissionId.ToString(),
                ["status"] = domainEvent.Status.ToString(),
                ["version"] = domainEvent.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["premium"] = domainEvent.Premium.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["expiresAtUtc"] = (domainEvent.ExpiresAtUtc ?? domainEvent.OccurredAtUtc).ToString("O")
            }, domainEvent.SubmissionReference, domainEvent.CompanyName));
    }

    private static Dictionary<string, string> AddSubmissionContext(
        Dictionary<string, string> attributes,
        string? submissionReference,
        string? companyName)
    {
        if (!string.IsNullOrWhiteSpace(submissionReference))
            attributes["submissionReference"] = submissionReference;
        if (!string.IsNullOrWhiteSpace(companyName))
            attributes["companyName"] = companyName;

        return attributes;
    }
}

public sealed class QuoteUnderwritingDecisionRecordedNotificationMapper
    : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => nameof(QuoteUnderwritingDecisionRecordedDomainEvent);

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<QuoteUnderwritingDecisionRecordedDomainEvent>(outboxMessage);

        return NotificationMessageFactory.CreateMessage(
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
}

public sealed class QuoteAcceptedNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => nameof(QuoteAcceptedDomainEvent);

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<QuoteAcceptedDomainEvent>(outboxMessage);

        return NotificationMessageFactory.CreateMessage(
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
}

public sealed class PolicyBoundNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => nameof(PolicyBoundDomainEvent);

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<PolicyBoundDomainEvent>(outboxMessage);

        return NotificationMessageFactory.CreateMessage(
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
}
