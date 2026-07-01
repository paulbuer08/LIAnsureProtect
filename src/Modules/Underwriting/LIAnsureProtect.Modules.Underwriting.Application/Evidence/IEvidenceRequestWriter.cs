namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence;

/// <summary>
/// Inbound port for legacy document-coupled handlers to apply request-state changes to the module-owned
/// evidence request. Documents and clean-document gates stay legacy in Milestone 37.
/// </summary>
public interface IEvidenceRequestWriter
{
    Task<EvidenceRequestSnapshot?> RecordResponseAsync(
        Guid evidenceRequestId,
        string ownerUserId,
        string respondentName,
        string respondentTitle,
        string responseText,
        string? attachmentFileName,
        string? attachmentContentType,
        long? attachmentSizeBytes,
        DateTime respondedAtUtc,
        CancellationToken cancellationToken);

    Task<EvidenceRequestSnapshot?> RecordSupplementalResponseAsync(
        Guid evidenceRequestId,
        string ownerUserId,
        string respondentName,
        string respondentTitle,
        string responseText,
        string? attachmentFileName,
        string? attachmentContentType,
        long? attachmentSizeBytes,
        DateTime respondedAtUtc,
        CancellationToken cancellationToken);

    Task<EvidenceRequestSnapshot?> AcceptAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        string reviewedByUserId,
        string reason,
        int documentCount,
        int cleanDocumentCount,
        DateTime reviewedAtUtc,
        CancellationToken cancellationToken);

    Task<EvidenceRequestSnapshot?> RecordReviewDecisionAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        string decision,
        string reason,
        string? remediationGuidance,
        string reviewedByUserId,
        int documentCount,
        int cleanDocumentCount,
        DateTime reviewedAtUtc,
        CancellationToken cancellationToken);
}

public sealed record EvidenceRequestSnapshot(
    Guid EvidenceRequestId,
    Guid QuoteId,
    Guid SubmissionId,
    string Category,
    string Title,
    string Description,
    DateTime DueAtUtc,
    string Status,
    bool IsOverdue,
    int DaysUntilDue,
    string RequestedByUserId,
    DateTime RequestedAtUtc,
    string? RespondedByUserId,
    string? RespondentName,
    string? RespondentTitle,
    string? ResponseText,
    string? AttachmentFileName,
    string? AttachmentContentType,
    long? AttachmentSizeBytes,
    DateTime? RespondedAtUtc,
    string? AcceptedByUserId,
    DateTime? AcceptedAtUtc,
    string? CancelledByUserId,
    DateTime? CancelledAtUtc,
    string? ReviewNotes,
    string ReviewDecision,
    string? ReviewReason,
    string? RemediationGuidance,
    string? ReviewedByUserId,
    DateTime? ReviewedAtUtc,
    DateTime UpdatedAtUtc);
