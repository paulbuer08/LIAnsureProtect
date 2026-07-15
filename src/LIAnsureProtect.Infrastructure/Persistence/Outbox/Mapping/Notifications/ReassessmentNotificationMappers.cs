using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.Notifications;

public sealed class ReassessmentReviewRequestedNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => nameof(ReassessmentReviewRequestedDomainEvent);

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ReassessmentReviewRequestedDomainEvent>(outboxMessage);
        return NotificationMessageFactory.CreateMessage(
            outboxMessage,
            NotificationMessageTypes.ReassessmentReviewRequested,
            NotificationAudiences.UnderwritingOperations,
            domainEvent.OwnerUserId,
            "reassessment_request",
            domainEvent.ReassessmentRequestId,
            domainEvent.OccurredAtUtc,
            AddContext(new Dictionary<string, string>
            {
                ["reassessmentRequestId"] = domainEvent.ReassessmentRequestId.ToString(),
                ["submissionId"] = domainEvent.SubmissionId.ToString(),
                ["quoteId"] = domainEvent.BaseQuoteId.ToString(),
                ["quoteVersion"] = domainEvent.BaseQuoteVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["status"] = ReassessmentRequestStatus.Pending.ToString()
            }, domainEvent.SubmissionReference, domainEvent.CompanyName));
    }

    private static Dictionary<string, string> AddContext(
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

public sealed class ReassessmentReviewDecisionRecordedNotificationMapper : IOutboxMessageMapper<NotificationMessage>
{
    public string EventType => nameof(ReassessmentReviewDecisionRecordedDomainEvent);

    public NotificationMessage Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ReassessmentReviewDecisionRecordedDomainEvent>(outboxMessage);
        var attributes = new Dictionary<string, string>
        {
            ["reassessmentRequestId"] = domainEvent.ReassessmentRequestId.ToString(),
            ["submissionId"] = domainEvent.SubmissionId.ToString(),
            ["quoteId"] = (domainEvent.CreatedQuoteId ?? domainEvent.BaseQuoteId).ToString(),
            ["quoteVersion"] = (domainEvent.Status == ReassessmentRequestStatus.Approved.ToString()
                ? domainEvent.BaseQuoteVersion + 1
                : domainEvent.BaseQuoteVersion).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["status"] = domainEvent.Status,
            ["decisionReason"] = domainEvent.DecisionReason
        };
        if (!string.IsNullOrWhiteSpace(domainEvent.SubmissionReference))
            attributes["submissionReference"] = domainEvent.SubmissionReference;
        if (!string.IsNullOrWhiteSpace(domainEvent.CompanyName))
            attributes["companyName"] = domainEvent.CompanyName;

        return NotificationMessageFactory.CreateMessage(
            outboxMessage,
            NotificationMessageTypes.ReassessmentReviewDecisionRecorded,
            NotificationAudiences.CustomerOrBroker,
            domainEvent.OwnerUserId,
            "reassessment_request",
            domainEvent.ReassessmentRequestId,
            domainEvent.OccurredAtUtc,
            attributes);
    }
}
