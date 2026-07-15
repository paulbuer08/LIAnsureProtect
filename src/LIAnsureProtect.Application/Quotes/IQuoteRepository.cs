using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes;

public interface IQuoteRepository
{
    Task AddAsync(Quote quote, CancellationToken cancellationToken);

    Task AddRatingProviderAttemptAsync(
        QuoteRatingProviderAttempt attempt,
        CancellationToken cancellationToken);

    Task<Quote?> GetLatestOwnedForSubmissionAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<Quote?> GetOwnedForReadAsync(
        Guid submissionId,
        Guid quoteId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Quote>> ListOwnedForSubmissionAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Quote>> ListPendingReferralsAsync(CancellationToken cancellationToken);

    Task<Quote?> GetForUnderwritingReviewAsync(Guid quoteId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<QuoteUnderwritingReview>> ListUnderwritingReviewsAsync(
        Guid quoteId,
        CancellationToken cancellationToken);

    Task<Quote?> GetOwnedForAcceptanceAsync(
        Guid quoteId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<Quote?> GetOwnedForBindingAsync(
        Guid quoteId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task AddUnderwritingReviewAsync(QuoteUnderwritingReview review, CancellationToken cancellationToken);
}
