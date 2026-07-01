using LIAnsureProtect.Modules.Underwriting.Application.Evidence;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class EvidenceRequestWriter(UnderwritingDbContext dbContext) : IEvidenceRequestWriter
{
    public async Task<EvidenceRequestSnapshot?> RecordResponseAsync(
        Guid evidenceRequestId,
        string ownerUserId,
        string respondentName,
        string respondentTitle,
        string responseText,
        string? attachmentFileName,
        string? attachmentContentType,
        long? attachmentSizeBytes,
        DateTime respondedAtUtc,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await GetForOwnerAsync(evidenceRequestId, ownerUserId, cancellationToken);
        if (evidenceRequest is null)
            return null;

        evidenceRequest.Respond(
            ownerUserId,
            respondentName,
            respondentTitle,
            responseText,
            attachmentFileName,
            attachmentContentType,
            attachmentSizeBytes,
            respondedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSnapshot(evidenceRequest);
    }

    public Task<EvidenceRequestSnapshot?> RecordSupplementalResponseAsync(
        Guid evidenceRequestId,
        string ownerUserId,
        string respondentName,
        string respondentTitle,
        string responseText,
        string? attachmentFileName,
        string? attachmentContentType,
        long? attachmentSizeBytes,
        DateTime respondedAtUtc,
        CancellationToken cancellationToken)
    {
        return RecordResponseAsync(
            evidenceRequestId,
            ownerUserId,
            respondentName,
            respondentTitle,
            responseText,
            attachmentFileName,
            attachmentContentType,
            attachmentSizeBytes,
            respondedAtUtc,
            cancellationToken);
    }

    public async Task<EvidenceRequestSnapshot?> AcceptAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        string reviewedByUserId,
        string? reviewNotes,
        int documentCount,
        int cleanDocumentCount,
        DateTime reviewedAtUtc,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await GetForUnderwritingAsync(quoteId, evidenceRequestId, cancellationToken);
        if (evidenceRequest is null)
            return null;

        evidenceRequest.Accept(reviewedByUserId, reviewNotes, reviewedAtUtc);
        var review = QuoteEvidenceRequestReview.Record(
            evidenceRequest,
            EvidenceReviewDecisionStatus.Satisfied,
            reviewNotes ?? "Evidence accepted by underwriting.",
            null,
            reviewedByUserId,
            reviewedAtUtc,
            documentCount,
            cleanDocumentCount);
        await dbContext.Set<QuoteEvidenceRequestReview>().AddAsync(review, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSnapshot(evidenceRequest);
    }

    public async Task<EvidenceRequestSnapshot?> RecordReviewDecisionAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        string decision,
        string reason,
        string? remediationGuidance,
        string reviewedByUserId,
        int documentCount,
        int cleanDocumentCount,
        DateTime reviewedAtUtc,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await GetForUnderwritingAsync(quoteId, evidenceRequestId, cancellationToken);
        if (evidenceRequest is null)
            return null;

        if (!Enum.TryParse<EvidenceReviewDecisionStatus>(decision, ignoreCase: false, out var parsedDecision))
            throw new ArgumentException("Evidence review decision is not supported.", nameof(decision));

        evidenceRequest.RecordReviewDecision(
            parsedDecision,
            reason,
            remediationGuidance,
            reviewedByUserId,
            reviewedAtUtc);
        var review = QuoteEvidenceRequestReview.Record(
            evidenceRequest,
            parsedDecision,
            reason,
            remediationGuidance,
            reviewedByUserId,
            reviewedAtUtc,
            documentCount,
            cleanDocumentCount);
        await dbContext.Set<QuoteEvidenceRequestReview>().AddAsync(review, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSnapshot(evidenceRequest);
    }

    private Task<QuoteEvidenceRequest?> GetForOwnerAsync(
        Guid evidenceRequestId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<QuoteEvidenceRequest>().SingleOrDefaultAsync(
            request => request.Id == evidenceRequestId && request.OwnerUserId == ownerUserId,
            cancellationToken);
    }

    private Task<QuoteEvidenceRequest?> GetForUnderwritingAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<QuoteEvidenceRequest>().SingleOrDefaultAsync(
            request => request.Id == evidenceRequestId && request.QuoteId == quoteId,
            cancellationToken);
    }

    private static EvidenceRequestSnapshot ToSnapshot(QuoteEvidenceRequest request)
    {
        return new EvidenceRequestSnapshot(
            request.Id,
            request.QuoteId,
            request.SubmissionId,
            request.OwnerUserId,
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
            request.UpdatedAtUtc);
    }
}
