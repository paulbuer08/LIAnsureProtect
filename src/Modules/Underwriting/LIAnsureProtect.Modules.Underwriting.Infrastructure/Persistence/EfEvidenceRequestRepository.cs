using LIAnsureProtect.Modules.Underwriting.Application.Evidence;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class EfEvidenceRequestRepository(UnderwritingDbContext dbContext) : IEvidenceRequestRepository
{
    public async Task AddAsync(QuoteEvidenceRequest evidenceRequest, CancellationToken cancellationToken)
        => await dbContext.Set<QuoteEvidenceRequest>().AddAsync(evidenceRequest, cancellationToken);

    public async Task AddReviewAsync(QuoteEvidenceRequestReview review, CancellationToken cancellationToken)
        => await dbContext.Set<QuoteEvidenceRequestReview>().AddAsync(review, cancellationToken);

    public async Task AddResponseAsync(QuoteEvidenceResponse response, CancellationToken cancellationToken)
        => await dbContext.Set<QuoteEvidenceResponse>().AddAsync(response, cancellationToken);

    public async Task<IReadOnlyCollection<QuoteEvidenceResponse>> ListResponsesAsync(
        Guid evidenceRequestId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<QuoteEvidenceResponse>()
            .AsNoTracking()
            .Where(response => response.EvidenceRequestId == evidenceRequestId)
            .OrderBy(response => response.RespondedAtUtc)
            .ThenBy(response => response.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountPendingFollowUpsAsync(
        Guid evidenceRequestId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<QuoteEvidenceResponse>().CountAsync(
            response => response.EvidenceRequestId == evidenceRequestId
                && response.Kind == EvidenceResponseKind.FollowUp
                && response.ViewedAtUtc == null,
            cancellationToken);
    }

    public Task<QuoteEvidenceResponse?> GetResponseForUnderwritingAsync(
        Guid evidenceRequestId,
        Guid responseId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<QuoteEvidenceResponse>().SingleOrDefaultAsync(
            response => response.Id == responseId
                && response.EvidenceRequestId == evidenceRequestId,
            cancellationToken);
    }

    public Task<QuoteEvidenceResponse?> GetResponseForOwnerAsync(
        Guid evidenceRequestId,
        Guid responseId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<QuoteEvidenceResponse>().SingleOrDefaultAsync(
            response => response.Id == responseId
                && response.EvidenceRequestId == evidenceRequestId
                && response.OwnerUserId == ownerUserId,
            cancellationToken);
    }

    public Task<QuoteEvidenceRequest?> GetForUnderwritingAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<QuoteEvidenceRequest>().SingleOrDefaultAsync(
            request => request.Id == evidenceRequestId && request.QuoteId == quoteId,
            cancellationToken);
    }

    public Task<QuoteEvidenceRequest?> GetForOwnerAsync(
        Guid evidenceRequestId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<QuoteEvidenceRequest>().SingleOrDefaultAsync(
            request => request.Id == evidenceRequestId && request.OwnerUserId == ownerUserId,
            cancellationToken);
    }

    public Task<bool> ExistsForQuoteCategoryAsync(
        Guid quoteId,
        string category,
        CancellationToken cancellationToken)
    {
        return Enum.TryParse<EvidenceRequestCategory>(category, out var parsed)
            ? dbContext.Set<QuoteEvidenceRequest>().AnyAsync(
                request => request.QuoteId == quoteId && request.Category == parsed,
                cancellationToken)
            : Task.FromResult(false);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException(
                "The evidence request changed while this action was being saved. Refresh and try again.",
                exception);
        }
    }
}
