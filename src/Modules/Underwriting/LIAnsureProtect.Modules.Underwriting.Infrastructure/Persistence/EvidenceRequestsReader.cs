using LIAnsureProtect.Modules.Underwriting.Application.Evidence;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class EvidenceRequestsReader(UnderwritingDbContext dbContext) : IEvidenceRequestsReader
{
    public async Task<EvidenceRequestSnapshot?> GetOwnerRequestAsync(
        Guid evidenceRequestId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.Set<QuoteEvidenceRequest>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                evidenceRequest => evidenceRequest.Id == evidenceRequestId
                    && evidenceRequest.OwnerUserId == ownerUserId,
                cancellationToken);

        return request is null ? null : ToSnapshot(request, DateTime.UtcNow);
    }

    public async Task<EvidenceRequestSnapshot?> GetUnderwritingRequestAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.Set<QuoteEvidenceRequest>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                evidenceRequest => evidenceRequest.Id == evidenceRequestId
                    && evidenceRequest.QuoteId == quoteId,
                cancellationToken);

        return request is null ? null : ToSnapshot(request, DateTime.UtcNow);
    }

    public async Task<IReadOnlyCollection<EvidenceRequestOwnerItem>> GetOwnerRequestsAsync(
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var requests = await dbContext.Set<QuoteEvidenceRequest>()
            .AsNoTracking()
            .Where(request => request.OwnerUserId == ownerUserId)
            .OrderBy(request => request.DueAtUtc)
            .ThenByDescending(request => request.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        return requests.Select(request => ToOwnerItem(request, nowUtc)).ToList();
    }

    public async Task<IReadOnlyCollection<EvidenceRequestSummaryItem>> GetSummariesAsync(
        IReadOnlyCollection<Guid> quoteIds,
        CancellationToken cancellationToken)
    {
        if (quoteIds.Count == 0)
            return [];

        var requests = await dbContext.Set<QuoteEvidenceRequest>()
            .AsNoTracking()
            .Where(request => quoteIds.Contains(request.QuoteId))
            .ToListAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        return requests
            .GroupBy(request => request.QuoteId)
            .Select(group => CreateSummary(group.Key, group.ToList(), nowUtc))
            .ToList();
    }

    private static EvidenceRequestSnapshot ToSnapshot(QuoteEvidenceRequest request, DateTime nowUtc)
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
            request.Status == EvidenceRequestStatus.Open && request.DueAtUtc < nowUtc,
            (request.DueAtUtc.Date - nowUtc.Date).Days,
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

    private static EvidenceRequestOwnerItem ToOwnerItem(QuoteEvidenceRequest request, DateTime nowUtc)
    {
        return new EvidenceRequestOwnerItem(
            request.Id,
            request.QuoteId,
            request.SubmissionId,
            request.Category.ToString(),
            request.Title,
            request.Description,
            request.DueAtUtc,
            request.Status.ToString(),
            request.Status == EvidenceRequestStatus.Open && request.DueAtUtc < nowUtc,
            (request.DueAtUtc.Date - nowUtc.Date).Days,
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

    private static EvidenceRequestSummaryItem CreateSummary(
        Guid quoteId,
        IReadOnlyCollection<QuoteEvidenceRequest> requests,
        DateTime nowUtc)
    {
        var openRequests = requests
            .Where(request => request.Status == EvidenceRequestStatus.Open)
            .ToList();

        return new EvidenceRequestSummaryItem(
            quoteId,
            openRequests.Count,
            requests.Count(request => request.Status == EvidenceRequestStatus.Responded),
            requests.Count(request =>
                request.Status == EvidenceRequestStatus.Responded
                && request.ReviewDecision == EvidenceReviewDecisionStatus.NotReviewed),
            requests.Count(request => request.ReviewDecision == EvidenceReviewDecisionStatus.Satisfied),
            requests.Count(request =>
                request.ReviewDecision is EvidenceReviewDecisionStatus.Insufficient
                    or EvidenceReviewDecisionStatus.NeedsClarification),
            openRequests.Count(request => request.DueAtUtc < nowUtc),
            openRequests
                .OrderBy(request => request.DueAtUtc)
                .Select(request => (DateTime?)request.DueAtUtc)
                .FirstOrDefault(),
            requests.Any(request => request.Status is EvidenceRequestStatus.Open or EvidenceRequestStatus.Responded),
            requests
                .OrderByDescending(request => request.UpdatedAtUtc)
                .Select(request => (DateTime?)request.UpdatedAtUtc)
                .FirstOrDefault());
    }
}
