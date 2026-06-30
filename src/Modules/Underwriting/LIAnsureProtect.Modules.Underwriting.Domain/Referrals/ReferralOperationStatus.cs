namespace LIAnsureProtect.Modules.Underwriting.Domain.Referrals;

public enum ReferralOperationStatus
{
    New,
    InReview,
    WaitingForInformation,
    Escalated,
    ReadyForDecision,
    Closed
}
