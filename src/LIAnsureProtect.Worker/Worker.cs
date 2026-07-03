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
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
                var processedCount = await dispatcher.DispatchPendingMessagesAsync(stoppingToken);

                if (processedCount > 0)
                {
                    WorkerLog.ProcessedOutboxMessages(logger, processedCount);
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
                        WorkerLog.DeletedIdempotencyRecords(logger, deletedCount);
                    }

                    nextIdempotencyCleanupAtUtc = nowUtc.Add(IdempotencyCleanupInterval);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                // A transient failure (database restart, network blip) must not stop the host:
                // log it and try again on the next poll. Shutdown cancellation still propagates.
                WorkerLog.PollIterationFailed(logger, exception);
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
