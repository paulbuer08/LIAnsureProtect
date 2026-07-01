using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence;

public interface IEvidenceRequestRepository
{
    Task AddAsync(QuoteEvidenceRequest evidenceRequest, CancellationToken cancellationToken);

    Task AddReviewAsync(QuoteEvidenceRequestReview review, CancellationToken cancellationToken);

    Task<QuoteEvidenceRequest?> GetForUnderwritingAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        CancellationToken cancellationToken);

    Task<QuoteEvidenceRequest?> GetForOwnerAsync(
        Guid evidenceRequestId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
