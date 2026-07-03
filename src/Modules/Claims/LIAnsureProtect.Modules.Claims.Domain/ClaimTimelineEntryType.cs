namespace LIAnsureProtect.Modules.Claims.Domain;

/// <summary>Categories for the claim's append-only timeline (the story an auditor reads).</summary>
public enum ClaimTimelineEntryType
{
    ClaimFiled,
    StatusChanged,
    AssignmentChanged,
    NoteAdded,
    InformationRequested,
    ClaimantResponded,
    DocumentUploaded,
    ClaimedAmountUpdated,
    ReserveChanged
}
