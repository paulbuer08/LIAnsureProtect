namespace LIAnsureProtect.Domain.Quotes;

public enum ReferralTimelineEntryType
{
    OperationCreated,
    AssignmentChanged,
    PriorityChanged,
    DueDateChanged,
    StatusChanged,
    NoteAdded,
    TaskAdded,
    TaskCompleted,
    EvidenceRequestCreated,
    EvidenceRequestResponded,
    EvidenceRequestAccepted,
    EvidenceRequestCancelled,
    EvidenceRequestFollowUpSent,
    EvidenceRequestReviewDecisionRecorded,
    DecisionRecorded
}
