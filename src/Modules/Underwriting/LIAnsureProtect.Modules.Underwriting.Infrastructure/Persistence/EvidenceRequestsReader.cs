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

    public async Task<IReadOnlyCollection<EvidenceRequestOwnerSummaryItem>> GetOwnerRequestsPageAsync(
        string ownerUserId,
        EvidenceRequestStatus? status,
        EvidenceRequestCategory? category,
        Guid? quoteId,
        bool? overdue,
        string? search,
        EvidenceReviewDecisionStatus? reviewDecision,
        EvidenceDocumentRequirement? documentRequirement,
        QuoteEvidenceDisposition? quoteDisposition,
        DateTime? cursorDueAtUtc,
        DateTime? cursorRequestedAtUtc,
        Guid? cursorEvidenceRequestId,
        int take,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var query = dbContext.Set<QuoteEvidenceRequest>()
            .AsNoTracking()
            .Where(request => request.OwnerUserId == ownerUserId);

        if (status.HasValue)
            query = query.Where(request => request.Status == status.Value);

        if (category.HasValue)
            query = query.Where(request => request.Category == category.Value);

        if (quoteId.HasValue)
            query = query.Where(request => request.QuoteId == quoteId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.ToUpperInvariant()}%";
            var exactId = Guid.TryParse(search, out var parsedId) ? parsedId : (Guid?)null;
#pragma warning disable CA1304, CA1311 // Translated by EF into provider-side UPPER.
            query = query.Where(request =>
                EF.Functions.Like(request.Title.ToUpper(), pattern)
                || EF.Functions.Like(request.Description.ToUpper(), pattern)
                || EF.Functions.Like(request.SubmissionReference.ToUpper(), pattern)
                || EF.Functions.Like(request.CompanyName.ToUpper(), pattern)
                || (exactId.HasValue && (request.Id == exactId.Value
                    || request.QuoteId == exactId.Value
                    || request.SubmissionId == exactId.Value)));
#pragma warning restore CA1304, CA1311
        }

        if (reviewDecision.HasValue)
            query = query.Where(request => request.ReviewDecision == reviewDecision.Value);

        if (documentRequirement.HasValue)
            query = query.Where(request => request.DocumentRequirement == documentRequirement.Value);

        if (quoteDisposition.HasValue)
            query = query.Where(request => request.QuoteDisposition == quoteDisposition.Value);

        if (overdue == true)
            query = query.Where(request => request.Status == EvidenceRequestStatus.Open && request.DueAtUtc < nowUtc);
        else if (overdue == false)
            query = query.Where(request => request.Status != EvidenceRequestStatus.Open || request.DueAtUtc >= nowUtc);

        if (cursorDueAtUtc.HasValue
            && cursorRequestedAtUtc.HasValue
            && cursorEvidenceRequestId.HasValue)
        {
            query = query.Where(request =>
                request.DueAtUtc > cursorDueAtUtc.Value
                || (request.DueAtUtc == cursorDueAtUtc.Value
                    && request.RequestedAtUtc < cursorRequestedAtUtc.Value)
                || (request.DueAtUtc == cursorDueAtUtc.Value
                    && request.RequestedAtUtc == cursorRequestedAtUtc.Value
                    && request.Id.CompareTo(cursorEvidenceRequestId.Value) > 0));
        }

        var requests = await query
            .OrderBy(request => request.DueAtUtc)
            .ThenByDescending(request => request.RequestedAtUtc)
            .ThenBy(request => request.Id)
            .Take(take)
            .Select(request => new EvidenceRequestOwnerSummaryItem(
                request.Id,
                request.QuoteId,
                request.SubmissionId,
                request.Category,
                request.Title,
                request.Description,
                request.DueAtUtc,
                request.Status,
                false,
                0,
                request.RequestedAtUtc,
                request.ReviewDecision,
                request.RemediationGuidance,
                request.UpdatedAtUtc,
                request.SubmissionReference,
                request.CompanyName,
                request.DocumentRequirement,
                request.QuoteVersion,
                request.QuoteDisposition,
                request.SupersededAtUtc,
                request.SupersededByQuoteId,
                request.SupersededByQuoteVersion))
            .ToListAsync(cancellationToken);

        return requests.Select(request => request with
        {
            IsOverdue = request.Status == EvidenceRequestStatus.Open && request.DueAtUtc < nowUtc,
            DaysUntilDue = (request.DueAtUtc.Date - nowUtc.Date).Days
        }).ToList();
    }

    public async Task<IReadOnlyCollection<EvidenceRequestSummaryItem>> GetSummariesAsync(
        IReadOnlyCollection<Guid> quoteIds,
        CancellationToken cancellationToken)
    {
        if (quoteIds.Count == 0)
            return [];

        var requests = await dbContext.Set<QuoteEvidenceRequest>()
            .AsNoTracking()
            .Where(request => quoteIds.Contains(request.QuoteId)
                && request.QuoteDisposition == QuoteEvidenceDisposition.Current)
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
            request.SubmissionReference,
            request.CompanyName,
            request.DocumentRequirement.ToString(),
            request.RespondentMobileNumber,
            request.RespondentTelephoneNumber,
            request.QuoteVersion,
            request.QuoteDisposition.ToString(),
            request.SupersededAtUtc,
            request.SupersededByQuoteId,
            request.SupersededByQuoteVersion);
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
