using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using ModuleEvidenceRequestCategory = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.EvidenceRequestCategory;
using ModuleEvidenceRequestStatus = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.EvidenceRequestStatus;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.Notifications;

internal static class NotificationMessageFactory
{
    public static NotificationMessage CreateEvidenceMessage(
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
        Dictionary<string, string> extraAttributes,
        string? submissionReference = null,
        string? companyName = null)
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

        if (!string.IsNullOrWhiteSpace(submissionReference))
            attributes["submissionReference"] = submissionReference;
        if (!string.IsNullOrWhiteSpace(companyName))
            attributes["companyName"] = companyName;

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

    public static NotificationMessage CreateMessage(
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
}
