using LIAnsureProtect.Infrastructure.Persistence.Idempotency;
using LIAnsureProtect.Infrastructure.Persistence.Outbox;

namespace LIAnsureProtect.Worker;

public sealed class Worker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<Worker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IdempotencyCleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan CompletedIdempotencyRecordRetention = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextIdempotencyCleanupAtUtc = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
            var processedCount = await dispatcher.DispatchPendingMessagesAsync(stoppingToken);

            if (processedCount > 0)
            {
                logger.LogInformation(
                    "Processed {ProcessedOutboxMessageCount} outbox message(s).",
                    processedCount);
            }

            var nowUtc = DateTime.UtcNow;
            if (nowUtc >= nextIdempotencyCleanupAtUtc)
            {
                var cleanup = scope.ServiceProvider.GetRequiredService<IIdempotencyRecordCleanup>();
                var deletedCount = await cleanup.DeleteExpiredCompletedRecordsAsync(
                    nowUtc.Subtract(CompletedIdempotencyRecordRetention),
                    stoppingToken);

                if (deletedCount > 0)
                {
                    logger.LogInformation(
                        "Deleted {DeletedIdempotencyRecordCount} expired completed idempotency record(s).",
                        deletedCount);
                }

                nextIdempotencyCleanupAtUtc = nowUtc.Add(IdempotencyCleanupInterval);
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
