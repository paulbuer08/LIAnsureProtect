using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence.Documents;

namespace LIAnsureProtect.Application.Quotes;

public interface IQuoteRepository
{
    Task AddAsync(Quote quote, CancellationToken cancellationToken);

    Task AddRatingProviderAttemptAsync(
        QuoteRatingProviderAttempt attempt,
        CancellationToken cancellationToken);

    Task AddEvidenceDocumentsAsync(
        IReadOnlyCollection<QuoteEvidenceDocument> evidenceDocuments,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Quote>> ListPendingReferralsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<QuoteEvidenceDocument>> ListEvidenceDocumentsForRequestsAsync(
        IReadOnlyCollection<Guid> evidenceRequestIds,
        CancellationToken cancellationToken);

    Task<QuoteEvidenceDocument?> GetEvidenceDocumentForOwnerAsync(
        Guid evidenceRequestId,
        Guid documentId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<QuoteEvidenceDocument?> GetEvidenceDocumentForUnderwritingAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        Guid documentId,
        CancellationToken cancellationToken);

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
