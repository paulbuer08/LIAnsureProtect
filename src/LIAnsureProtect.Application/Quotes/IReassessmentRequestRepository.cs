using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes;

public interface IReassessmentRequestRepository
{
    Task AddAsync(ReassessmentRequest request, CancellationToken cancellationToken);

    Task<ReassessmentRequest?> GetPendingOwnedAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<ReassessmentRequest?> GetForReviewAsync(Guid requestId, CancellationToken cancellationToken);

    Task<int> CountSuccessfulOwnedAsync(
        Guid submissionId,
        string ownerUserId,
        DateTime? createdSinceUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ReassessmentRequest>> ListForReviewAsync(
        ReassessmentRequestStatus? status,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ReassessmentRequest>> ListOwnedAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken);
}
