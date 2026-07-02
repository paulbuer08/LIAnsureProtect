using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

public sealed class OutboxDispatcher(
    IEnumerable<IOutboxSource> sources,
    IEnumerable<IOutboxMessageConsumer> consumers) : IOutboxDispatcher
{
    private const int BatchSize = 20;
    private const int MaxPublishAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    public async Task<int> DispatchPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var sourceList = sources.ToList();
        var consumerList = consumers.ToList();
        var pendingMessages = new List<(IOutboxMessageView Message, IOutboxSource Source)>();

        foreach (var source in sourceList)
        {
            foreach (var message in await source.GetPendingAsync(BatchSize, nowUtc, cancellationToken))
            {
                pendingMessages.Add((message, source));
            }
        }

        if (pendingMessages.Count == 0)
            return 0;

        var orderedMessages = pendingMessages
            .OrderBy(item => item.Message.CreatedAtUtc)
            .ToList();
        var touchedSources = new HashSet<IOutboxSource>();
        var processedCount = 0;

        foreach (var (message, source) in orderedMessages)
        {
            touchedSources.Add(source);

            var providerMessageId = string.Empty;
            var failed = false;

            foreach (var consumer in consumerList)
            {
                var result = await consumer.ConsumeAsync(message, nowUtc, cancellationToken);
                if (result.Status == OutboxMessageConsumerStatus.NotHandled)
                    continue;

                if (result.Status == OutboxMessageConsumerStatus.Succeeded)
                {
                    if (!string.IsNullOrWhiteSpace(result.ProviderMessageId))
                        providerMessageId = result.ProviderMessageId;

                    continue;
                }

                var nextAttemptNumber = message.PublishAttemptCount + 1;
                var exhausted = result.Status == OutboxMessageConsumerStatus.PermanentFailure
                    || nextAttemptNumber >= MaxPublishAttempts;
                message.MarkPublishFailed(
                    nowUtc,
                    result.FailureReason ?? "Outbox message consumer failed.",
                    exhausted ? null : nowUtc.Add(RetryDelay),
                    exhausted);
                failed = true;
                break;
            }

            if (failed)
                continue;

            if (string.IsNullOrWhiteSpace(providerMessageId))
                message.MarkProcessed(nowUtc);
            else
                message.MarkPublishSucceeded(nowUtc, providerMessageId);

            processedCount++;
        }

        foreach (var source in touchedSources)
        {
            await source.SaveChangesAsync(cancellationToken);
        }

        return processedCount;
    }
}
