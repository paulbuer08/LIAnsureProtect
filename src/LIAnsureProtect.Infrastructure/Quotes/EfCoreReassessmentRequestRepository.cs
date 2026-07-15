using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Quotes;

public sealed class EfCoreReassessmentRequestRepository(SubmissionDbContext dbContext)
    : IReassessmentRequestRepository
{
    public async Task AddAsync(ReassessmentRequest request, CancellationToken cancellationToken)
        => await dbContext.ReassessmentRequests.AddAsync(request, cancellationToken);

    public Task<ReassessmentRequest?> GetPendingOwnedAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken)
        => dbContext.ReassessmentRequests.SingleOrDefaultAsync(
            request => request.SubmissionId == submissionId
                && request.OwnerUserId == ownerUserId
                && request.Status == ReassessmentRequestStatus.Pending,
            cancellationToken);

    public Task<ReassessmentRequest?> GetForReviewAsync(Guid requestId, CancellationToken cancellationToken)
        => dbContext.ReassessmentRequests.SingleOrDefaultAsync(request => request.Id == requestId, cancellationToken);

    public Task<int> CountSuccessfulOwnedAsync(
        Guid submissionId,
        string ownerUserId,
        DateTime? createdSinceUtc,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Quotes.AsNoTracking().Where(quote =>
            quote.SubmissionId == submissionId
            && quote.OwnerUserId == ownerUserId
            && quote.Version > 1);

        if (createdSinceUtc.HasValue)
            query = query.Where(quote => quote.CreatedAtUtc >= createdSinceUtc.Value);

        return query.CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ReassessmentRequest>> ListForReviewAsync(
        ReassessmentRequestStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ReassessmentRequests.AsNoTracking();
        if (status.HasValue)
            query = query.Where(request => request.Status == status.Value);

        return await query.OrderBy(request => request.RequestedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ReassessmentRequest>> ListOwnedAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken)
        => await dbContext.ReassessmentRequests
            .AsNoTracking()
            .Where(request => request.SubmissionId == submissionId && request.OwnerUserId == ownerUserId)
            .OrderByDescending(request => request.RequestedAtUtc)
            .ToListAsync(cancellationToken);
}
