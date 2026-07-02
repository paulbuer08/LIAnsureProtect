namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence;

public interface IEvidenceRequestsReader
{
    Task<EvidenceRequestSnapshot?> GetOwnerRequestAsync(
        Guid evidenceRequestId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<EvidenceRequestSnapshot?> GetUnderwritingRequestAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EvidenceRequestOwnerItem>> GetOwnerRequestsAsync(
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EvidenceRequestSummaryItem>> GetSummariesAsync(
        IReadOnlyCollection<Guid> quoteIds,
        CancellationToken cancellationToken);
}

public sealed record EvidenceRequestSnapshot(
    Guid EvidenceRequestId,
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
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

public sealed record EvidenceRequestOwnerItem(
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

public sealed record EvidenceRequestSummaryItem(
    Guid QuoteId,
    int OpenRequestCount,
    int RespondedRequestCount,
    int UnreviewedRespondedRequestCount,
    int SatisfiedRequestCount,
    int NeedsAttentionRequestCount,
    int OverdueRequestCount,
    DateTime? NextOpenDueAtUtc,
    bool IsWaitingForInformation,
    DateTime? LatestEvidenceActivityAtUtc);
