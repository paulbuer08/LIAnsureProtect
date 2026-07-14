using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence.Documents;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence;

public sealed record ListOwnerEvidenceRequestsResult(
    IReadOnlyCollection<EvidenceRequestOwnerSummaryResult> EvidenceRequests,
    string? NextCursor);

public sealed record EvidenceRequestOwnerSummaryResult(
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
    DateTime RequestedAtUtc,
    string ReviewDecision,
    string? RemediationGuidance,
    DateTime UpdatedAtUtc,
    string SubmissionReference = "",
    string CompanyName = "",
    string DocumentRequirement = "Required");

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
    string? RespondentEmail,
    string? RespondentPhone,
    string? ResponseText,
    string? OtherConcerns,
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
    IReadOnlyCollection<QuoteEvidenceDocumentResult> Documents,
    string SubmissionReference = "",
    string CompanyName = "",
    string DocumentRequirement = "Required",
    IReadOnlyCollection<QuoteEvidenceResponseResult>? Responses = null);

public sealed record QuoteEvidenceResponseResult(
    Guid ResponseId,
    string RespondedByUserId,
    string RespondentName,
    string RespondentTitle,
    string RespondentEmail,
    string? RespondentPhone,
    string? ResponseText,
    string? OtherConcerns,
    string Kind,
    DateTime RespondedAtUtc);

public sealed record QuoteEvidenceDocumentResult(
    Guid DocumentId,
    Guid? EvidenceResponseId,
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
    bool IsDownloadAvailable,
    string? AssessmentVersion,
    string? PlausibilityStatus,
    string? ClaimConsistencyStatus,
    IReadOnlyCollection<string> AdvisoryFindings);

internal static class QuoteEvidenceRequestResultFactory
{
    public static QuoteEvidenceRequestResult FromSnapshot(
        EvidenceRequestSnapshot item,
        IReadOnlyCollection<QuoteEvidenceDocument>? documents = null,
        IReadOnlyCollection<QuoteEvidenceResponse>? responses = null)
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
            item.RespondentEmail,
            item.RespondentPhone,
            item.ResponseText,
            item.OtherConcerns,
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
            (documents ?? []).OrderBy(document => document.UploadedAtUtc).Select(FromDocument).ToList(),
            item.SubmissionReference,
            item.CompanyName,
            item.DocumentRequirement,
            (responses ?? []).OrderBy(response => response.RespondedAtUtc).ThenBy(response => response.Id).Select(FromResponse).ToList());
    }

    public static QuoteEvidenceRequestResult FromRequest(
        QuoteEvidenceRequest request,
        IReadOnlyCollection<QuoteEvidenceDocument>? documents = null,
        IReadOnlyCollection<QuoteEvidenceResponse>? responses = null)
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
            request.RespondentEmail,
            request.RespondentPhone,
            request.ResponseText,
            request.OtherConcerns,
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
            (documents ?? [])
                .OrderBy(document => document.UploadedAtUtc)
                .Select(FromDocument)
                .ToList(),
            request.SubmissionReference,
            request.CompanyName,
            request.DocumentRequirement.ToString(),
            (responses ?? []).OrderBy(response => response.RespondedAtUtc).ThenBy(response => response.Id).Select(FromResponse).ToList());
    }

    private static QuoteEvidenceDocumentResult FromDocument(QuoteEvidenceDocument document)
    {
        return new QuoteEvidenceDocumentResult(
            document.Id,
            document.EvidenceResponseId,
            document.OriginalFileName,
            document.ContentType,
            document.SizeBytes,
            document.UploadedByUserId,
            document.UploadedAtUtc,
            document.ScanStatus.ToString(),
            document.ScannerProviderName,
            document.ScanResultCode,
            document.ScanResultReason,
            document.ScannedAtUtc,
            document.Sha256,
            document.IsDownloadAvailable,
            document.AssessmentVersion,
            document.PlausibilityStatus,
            document.ClaimConsistencyStatus,
            DeserializeFindings(document.AdvisoryFindingsJson));
    }

    private static QuoteEvidenceResponseResult FromResponse(QuoteEvidenceResponse response)
    {
        return new QuoteEvidenceResponseResult(
            response.Id,
            response.RespondedByUserId,
            response.RespondentName,
            response.RespondentTitle,
            response.RespondentEmail,
            response.RespondentPhone,
            response.ResponseText,
            response.OtherConcerns,
            response.Kind.ToString(),
            response.RespondedAtUtc);
    }

    private static string[] DeserializeFindings(string value)
    {
        return System.Text.Json.JsonSerializer.Deserialize<string[]>(value) ?? [];
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
