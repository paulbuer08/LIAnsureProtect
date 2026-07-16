namespace LIAnsureProtect.Modules.Notifications.Application;

// Maps a notification type to a short, human-friendly inbox title so the API is
// self-describing and the frontend does not need to know every type string.
public static class NotificationInboxTitles
{
    public static string For(string type, IReadOnlyDictionary<string, string>? attributes = null) => type switch
    {
        NotificationMessageTypes.QuoteReady => attributes?.TryGetValue("version", out var version) == true
            ? $"Quote version {version} is ready"
            : "Your quote is ready",
        NotificationMessageTypes.QuoteUnderwritingDecisionRecorded => "Underwriting decision recorded",
        NotificationMessageTypes.QuoteAccepted => "Quote accepted",
        NotificationMessageTypes.ReassessmentReviewRequested => "Reassessment review requested",
        NotificationMessageTypes.ReassessmentReviewDecisionRecorded => attributes?.TryGetValue("status", out var reassessmentStatus) == true
            ? $"Reassessment {reassessmentStatus.ToLowerInvariant()}"
            : "Reassessment review completed",
        NotificationMessageTypes.PolicyBound => "Your policy is bound",
        NotificationMessageTypes.EvidenceRequestCreated => attributes?.TryGetValue("requestTitle", out var requestTitle) == true
            ? $"Evidence requested: {requestTitle}"
            : "Evidence requested",
        NotificationMessageTypes.EvidenceRequestResponded => attributes?.TryGetValue("requestTitle", out var responseRequestTitle) == true
            ? $"Evidence response received: {responseRequestTitle}"
            : "Evidence response received",
        NotificationMessageTypes.EvidenceRequestAccepted => "Evidence accepted",
        NotificationMessageTypes.EvidenceRequestCancelled => "Evidence request cancelled",
        NotificationMessageTypes.EvidenceRequestFollowUpSent => "Evidence reminder",
        NotificationMessageTypes.EvidenceRequestRemediationRequired => "Action needed on your evidence",
        _ => "Notification"
    };
}
