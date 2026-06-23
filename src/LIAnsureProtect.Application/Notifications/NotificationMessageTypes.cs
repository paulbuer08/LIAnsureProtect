namespace LIAnsureProtect.Application.Notifications;

public static class NotificationMessageTypes
{
    public const string QuoteReady = "quote.ready";
    public const string QuoteReferredForUnderwriting = "quote.referred_for_underwriting";
    public const string QuoteUnderwritingDecisionRecorded = "quote.underwriting_decision_recorded";
    public const string QuoteAccepted = "quote.accepted";
    public const string PolicyBound = "policy.bound";
    public const string EvidenceRequestCreated = "evidence_request.created";
    public const string EvidenceRequestResponded = "evidence_request.responded";
    public const string EvidenceRequestAccepted = "evidence_request.accepted";
    public const string EvidenceRequestCancelled = "evidence_request.cancelled";
    public const string EvidenceRequestFollowUpSent = "evidence_request.follow_up_sent";
    public const string EvidenceRequestRemediationRequired = "evidence_request.remediation_required";
}
