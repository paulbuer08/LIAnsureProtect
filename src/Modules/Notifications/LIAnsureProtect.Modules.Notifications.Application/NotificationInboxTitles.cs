namespace LIAnsureProtect.Modules.Notifications.Application;

// Maps a notification type to a short, human-friendly inbox title so the API is
// self-describing and the frontend does not need to know every type string.
public static class NotificationInboxTitles
{
    public static string For(string type) => type switch
    {
        NotificationMessageTypes.QuoteReady => "Your quote is ready",
        NotificationMessageTypes.QuoteUnderwritingDecisionRecorded => "Underwriting decision recorded",
        NotificationMessageTypes.QuoteAccepted => "Quote accepted",
        NotificationMessageTypes.PolicyBound => "Your policy is bound",
        NotificationMessageTypes.EvidenceRequestCreated => "Evidence requested",
        NotificationMessageTypes.EvidenceRequestAccepted => "Evidence accepted",
        NotificationMessageTypes.EvidenceRequestCancelled => "Evidence request cancelled",
        NotificationMessageTypes.EvidenceRequestFollowUpSent => "Evidence reminder",
        NotificationMessageTypes.EvidenceRequestRemediationRequired => "Action needed on your evidence",
        _ => "Notification"
    };
}
