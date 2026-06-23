using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Quotes;

public sealed class EfCoreQuoteRepository(SubmissionDbContext dbContext) : IQuoteRepository
{
    public async Task AddAsync(Quote quote, CancellationToken cancellationToken)
    {
        await dbContext.Quotes.AddAsync(quote, cancellationToken);
    }

    public async Task AddRatingProviderAttemptAsync(
        QuoteRatingProviderAttempt attempt,
        CancellationToken cancellationToken)
    {
        await dbContext.QuoteRatingProviderAttempts.AddAsync(attempt, cancellationToken);
    }

    public async Task AddReferralOperationAsync(
        QuoteReferralOperation operation,
        CancellationToken cancellationToken)
    {
        await dbContext.QuoteReferralOperations.AddAsync(operation, cancellationToken);
    }

    public async Task AddEvidenceRequestAsync(
        QuoteEvidenceRequest evidenceRequest,
        CancellationToken cancellationToken)
    {
        await dbContext.QuoteEvidenceRequests.AddAsync(evidenceRequest, cancellationToken);
    }

    public async Task AddEvidenceDocumentsAsync(
        IReadOnlyCollection<QuoteEvidenceDocument> evidenceDocuments,
        CancellationToken cancellationToken)
    {
        await dbContext.QuoteEvidenceDocuments.AddRangeAsync(evidenceDocuments, cancellationToken);
    }

    public async Task AddEvidenceRequestReviewAsync(
        QuoteEvidenceRequestReview review,
        CancellationToken cancellationToken)
    {
        await dbContext.QuoteEvidenceRequestReviews.AddAsync(review, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Quote>> ListPendingReferralsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Quotes
            .AsNoTracking()
            .Where(quote => quote.Status == QuoteStatus.Referred)
            .OrderBy(quote => quote.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<QuoteReferralOperation>> ListReferralOperationsAsync(
        IReadOnlyCollection<Guid> quoteIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.QuoteReferralOperations
            .AsNoTracking()
            .Include(operation => operation.Notes)
            .Include(operation => operation.Tasks)
            .Include(operation => operation.TimelineEntries)
            .Where(operation => quoteIds.Contains(operation.QuoteId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<QuoteEvidenceRequest>> ListEvidenceRequestsForQuotesAsync(
        IReadOnlyCollection<Guid> quoteIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.QuoteEvidenceRequests
            .AsNoTracking()
            .Where(request => quoteIds.Contains(request.QuoteId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<QuoteEvidenceRequest>> ListEvidenceRequestsForOwnerAsync(
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.QuoteEvidenceRequests
            .AsNoTracking()
            .Where(request => request.OwnerUserId == ownerUserId)
            .OrderBy(request => request.DueAtUtc)
            .ThenByDescending(request => request.RequestedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<QuoteEvidenceDocument>> ListEvidenceDocumentsForRequestsAsync(
        IReadOnlyCollection<Guid> evidenceRequestIds,
        CancellationToken cancellationToken)
    {
        if (evidenceRequestIds.Count == 0)
            return [];

        return await dbContext.QuoteEvidenceDocuments
            .AsNoTracking()
            .Where(document => evidenceRequestIds.Contains(document.EvidenceRequestId))
            .OrderBy(document => document.UploadedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<QuoteEvidenceRequest?> GetEvidenceRequestForUnderwritingAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        CancellationToken cancellationToken)
    {
        return await dbContext.QuoteEvidenceRequests.SingleOrDefaultAsync(
            request => request.Id == evidenceRequestId && request.QuoteId == quoteId,
            cancellationToken);
    }

    public async Task<QuoteEvidenceRequest?> GetEvidenceRequestForOwnerAsync(
        Guid evidenceRequestId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.QuoteEvidenceRequests.SingleOrDefaultAsync(
            request => request.Id == evidenceRequestId && request.OwnerUserId == ownerUserId,
            cancellationToken);
    }

    public async Task<QuoteEvidenceDocument?> GetEvidenceDocumentForOwnerAsync(
        Guid evidenceRequestId,
        Guid documentId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.QuoteEvidenceDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                document => document.Id == documentId
                    && document.EvidenceRequestId == evidenceRequestId
                    && document.OwnerUserId == ownerUserId,
                cancellationToken);
    }

    public async Task<QuoteEvidenceDocument?> GetEvidenceDocumentForUnderwritingAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        return await dbContext.QuoteEvidenceDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                document => document.Id == documentId
                    && document.EvidenceRequestId == evidenceRequestId
                    && document.QuoteId == quoteId,
                cancellationToken);
    }

    public async Task<QuoteReferralOperation?> GetReferralOperationForUpdateAsync(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        return await dbContext.QuoteReferralOperations
            .Include(operation => operation.Notes)
            .Include(operation => operation.Tasks)
            .Include(operation => operation.TimelineEntries)
            .SingleOrDefaultAsync(
                operation => operation.QuoteId == quoteId,
                cancellationToken);
    }

    public async Task<Quote?> GetForUnderwritingReviewAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        return await dbContext.Quotes.SingleOrDefaultAsync(
            quote => quote.Id == quoteId,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<QuoteUnderwritingReview>> ListUnderwritingReviewsAsync(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        return await dbContext.QuoteUnderwritingReviews
            .AsNoTracking()
            .Where(review => review.QuoteId == quoteId)
            .OrderBy(review => review.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Quote?> GetOwnedForAcceptanceAsync(
        Guid quoteId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Quotes.SingleOrDefaultAsync(
            quote => quote.Id == quoteId && quote.OwnerUserId == ownerUserId,
            cancellationToken);
    }

    public async Task<Quote?> GetOwnedForBindingAsync(
        Guid quoteId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Quotes.SingleOrDefaultAsync(
            quote => quote.Id == quoteId && quote.OwnerUserId == ownerUserId,
            cancellationToken);
    }

    public async Task AddUnderwritingReviewAsync(
        QuoteUnderwritingReview review,
        CancellationToken cancellationToken)
    {
        await dbContext.QuoteUnderwritingReviews.AddAsync(review, cancellationToken);
    }

    public async Task AddAiUnderwritingReviewAsync(
        AiUnderwritingReview review,
        CancellationToken cancellationToken)
    {
        await dbContext.AiUnderwritingReviews.AddAsync(review, cancellationToken);
    }
}
