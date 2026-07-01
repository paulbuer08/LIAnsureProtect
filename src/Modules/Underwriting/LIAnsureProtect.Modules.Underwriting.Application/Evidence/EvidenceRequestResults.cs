using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence;

public sealed record ListOwnerEvidenceRequestsResult(
    IReadOnlyCollection<QuoteEvidenceRequestResult> EvidenceRequests);

public sealed record QuoteEvidenceRequestResult(
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
    DateTime UpdatedAtUtc,
    IReadOnlyCollection<QuoteEvidenceDocumentResult> Documents);

public sealed record QuoteEvidenceDocumentResult(
    Guid DocumentId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string UploadedByUserId,
    DateTime UploadedAtUtc,
    string ScanStatus,
    string? ScannerProviderName,
    string? ScanResultCode,
    string? ScanResultReason,
    DateTime? ScannedAtUtc,
    string? Sha256,
    bool IsDownloadAvailable);

internal static class QuoteEvidenceRequestResultFactory
{
    public static QuoteEvidenceRequestResult FromRequest(QuoteEvidenceRequest request)
    {
        return new QuoteEvidenceRequestResult(
            request.Id,
            request.QuoteId,
            request.SubmissionId,
            request.Category.ToString(),
            request.Title,
            request.Description,
            request.DueAtUtc,
            request.Status.ToString(),
            request.Status == EvidenceRequestStatus.Open && request.DueAtUtc < DateTime.UtcNow,
            (request.DueAtUtc.Date - DateTime.UtcNow.Date).Days,
            request.RequestedByUserId,
            request.RequestedAtUtc,
            request.RespondedByUserId,
            request.RespondentName,
            request.RespondentTitle,
            request.ResponseText,
            request.AttachmentFileName,
            request.AttachmentContentType,
            request.AttachmentSizeBytes,
            request.RespondedAtUtc,
            request.AcceptedByUserId,
            request.AcceptedAtUtc,
            request.CancelledByUserId,
            request.CancelledAtUtc,
            request.ReviewNotes,
            request.ReviewDecision.ToString(),
            request.ReviewReason,
            request.RemediationGuidance,
            request.ReviewedByUserId,
            request.ReviewedAtUtc,
            request.UpdatedAtUtc,
            []);
    }

    public static QuoteEvidenceRequestResult FromOwnerItem(EvidenceRequestOwnerItem item)
    {
        return new QuoteEvidenceRequestResult(
            item.EvidenceRequestId,
            item.QuoteId,
            item.SubmissionId,
            item.Category,
            item.Title,
            item.Description,
            item.DueAtUtc,
            item.Status,
            item.IsOverdue,
            item.DaysUntilDue,
            item.RequestedByUserId,
            item.RequestedAtUtc,
            item.RespondedByUserId,
            item.RespondentName,
            item.RespondentTitle,
            item.ResponseText,
            item.AttachmentFileName,
            item.AttachmentContentType,
            item.AttachmentSizeBytes,
            item.RespondedAtUtc,
            item.AcceptedByUserId,
            item.AcceptedAtUtc,
            item.CancelledByUserId,
            item.CancelledAtUtc,
            item.ReviewNotes,
            item.ReviewDecision,
            item.ReviewReason,
            item.RemediationGuidance,
            item.ReviewedByUserId,
            item.ReviewedAtUtc,
            item.UpdatedAtUtc,
            []);
    }
}

internal static class CurrentEvidenceUser
{
    public static string GetRequiredUserId(
        Platform.Abstractions.Security.ICurrentUser currentUser,
        string message)
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException(message)
            : currentUser.UserId;
    }
}
