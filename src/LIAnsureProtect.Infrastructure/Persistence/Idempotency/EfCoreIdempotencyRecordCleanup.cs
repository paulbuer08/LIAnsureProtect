using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Persistence.Idempotency;

public sealed class EfCoreIdempotencyRecordCleanup(SubmissionDbContext dbContext) : IIdempotencyRecordCleanup
{
    public async Task<int> DeleteExpiredCompletedRecordsAsync(
        DateTime completedBeforeUtc,
        CancellationToken cancellationToken)
    {
        var expiredCompletedRecords = await dbContext.IdempotencyRecords
            .Where(record =>
                record.Status == IdempotencyRecordStatus.Completed
                && record.CompletedAtUtc != null
                && record.CompletedAtUtc < completedBeforeUtc)
            .ToListAsync(cancellationToken);

        if (expiredCompletedRecords.Count == 0)
            return 0;

        dbContext.IdempotencyRecords.RemoveRange(expiredCompletedRecords);
        await dbContext.SaveChangesAsync(cancellationToken);

        return expiredCompletedRecords.Count;
    }
}
