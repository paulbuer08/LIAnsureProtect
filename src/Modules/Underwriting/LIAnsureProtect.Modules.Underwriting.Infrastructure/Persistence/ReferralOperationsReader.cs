using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class ReferralOperationsReader(UnderwritingDbContext dbContext) : IReferralOperationsReader
{
    public async Task<IReadOnlyCollection<ReferralOperationSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> quoteIds,
        CancellationToken cancellationToken)
    {
        if (quoteIds.Count == 0)
            return [];

        var operations = await dbContext.QuoteReferralOperations
            .AsNoTracking()
            .Include(operation => operation.Tasks)
            .Include(operation => operation.TimelineEntries)
            .Where(operation => quoteIds.Contains(operation.QuoteId))
            .ToListAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        return operations.Select(operation => ToSummary(operation, nowUtc)).ToList();
    }

    public async Task<IReadOnlyCollection<ReferralOperationTimelineItem>?> GetTimelineAsync(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        var operation = await dbContext.QuoteReferralOperations
            .AsNoTracking()
            .Include(candidate => candidate.TimelineEntries)
            .SingleOrDefaultAsync(candidate => candidate.QuoteId == quoteId, cancellationToken);

        if (operation is null)
            return null;

        return operation.TimelineEntries
            .Select(entry => new ReferralOperationTimelineItem(
                entry.EntryType.ToString(),
                entry.Summary,
                entry.CreatedByUserId,
                entry.CreatedAtUtc))
            .ToList();
    }

    private static ReferralOperationSummary ToSummary(QuoteReferralOperation operation, DateTime nowUtc)
        => new(
            operation.QuoteId,
            operation.AssignedUnderwriterUserId,
            operation.Priority.ToString(),
            operation.DueAtUtc,
            operation.DueAtUtc < nowUtc && operation.Status != ReferralOperationStatus.Closed,
            operation.Status.ToString(),
            operation.Tasks.Count(task => !task.IsCompleted),
            operation.TimelineEntries
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Select(entry => (DateTime?)entry.CreatedAtUtc)
                .FirstOrDefault());
}
