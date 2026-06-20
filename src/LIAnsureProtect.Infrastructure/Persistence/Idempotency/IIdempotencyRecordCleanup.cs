namespace LIAnsureProtect.Infrastructure.Persistence.Idempotency;

public interface IIdempotencyRecordCleanup
{
    Task<int> DeleteExpiredCompletedRecordsAsync(
        DateTime completedBeforeUtc,
        CancellationToken cancellationToken);
}
