using LIAnsureProtect.Infrastructure.Persistence.Outbox;

namespace LIAnsureProtect.Worker;

public sealed class Worker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<Worker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
