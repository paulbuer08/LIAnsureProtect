using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence;

public interface IEvidenceRequestRepository
{
    Task AddAsync(QuoteEvidenceRequest evidenceRequest, CancellationToken cancellationToken);

    Task AddReviewAsync(QuoteEvidenceRequestReview review, CancellationToken cancellationToken);

    Task AddResponseAsync(QuoteEvidenceResponse response, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<QuoteEvidenceResponse>> ListResponsesAsync(
        Guid evidenceRequestId,
        CancellationToken cancellationToken);

    Task<int> CountPendingFollowUpsAsync(
        Guid evidenceRequestId,
        CancellationToken cancellationToken);

    Task<QuoteEvidenceResponse?> GetResponseForUnderwritingAsync(
        Guid evidenceRequestId,
        Guid responseId,
        CancellationToken cancellationToken);

    Task<QuoteEvidenceResponse?> GetResponseForOwnerAsync(
        Guid evidenceRequestId,
        Guid responseId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<QuoteEvidenceRequest?> GetForUnderwritingAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        CancellationToken cancellationToken);

    Task<QuoteEvidenceRequest?> GetForOwnerAsync(
        Guid evidenceRequestId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<bool> ExistsForQuoteCategoryAsync(
        Guid quoteId,
        string category,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
