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

    public async Task<IReadOnlyCollection<Quote>> ListPendingReferralsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Quotes
            .AsNoTracking()
            .Where(quote => quote.Status == QuoteStatus.Referred)
            .OrderBy(quote => quote.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Quote?> GetForUnderwritingReviewAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        return await dbContext.Quotes.SingleOrDefaultAsync(
            quote => quote.Id == quoteId,
            cancellationToken);
    }

    public async Task AddUnderwritingReviewAsync(
        QuoteUnderwritingReview review,
        CancellationToken cancellationToken)
    {
        await dbContext.QuoteUnderwritingReviews.AddAsync(review, cancellationToken);
    }
}
