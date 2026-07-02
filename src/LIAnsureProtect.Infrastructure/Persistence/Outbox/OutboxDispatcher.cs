using System.Diagnostics;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using Microsoft.Extensions.Logging;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

public sealed class OutboxDispatcher(
    IEnumerable<IOutboxSource> sources,
    IEnumerable<IOutboxMessageConsumer> consumers,
    ILogger<OutboxDispatcher> logger) : IOutboxDispatcher
{
    private const int BatchSize = 20;
    private const int MaxPublishAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    public async Task<int> DispatchPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var batchActivity = OutboxDispatcherDiagnostics.ActivitySource.StartActivity(
            "OutboxDispatcher.DispatchPending");
        var stopwatch = Stopwatch.StartNew();
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

        batchActivity?.SetTag("outbox.source_count", sourceList.Count);
        batchActivity?.SetTag("outbox.pending_message_count", pendingMessages.Count);

        if (pendingMessages.Count == 0)
        {
            RecordBatchMetrics(pendingMessages.Count, processedCount: 0, failedCount: 0, stopwatch.Elapsed);
            return 0;
        }

        logger.LogInformation(
            "Dispatching {PendingMessageCount} outbox messages from {SourceCount} sources.",
            pendingMessages.Count,
            sourceList.Count);

        var orderedMessages = pendingMessages
            .OrderBy(item => item.Message.CreatedAtUtc)
            .ToList();
        var touchedSources = new HashSet<IOutboxSource>();
        var processedCount = 0;
        var failedCount = 0;

        foreach (var (message, source) in orderedMessages)
        {
            using var messageActivity = OutboxDispatcherDiagnostics.ActivitySource.StartActivity(
                "OutboxDispatcher.ProcessMessage");
            messageActivity?.SetTag("outbox.source", source.GetType().Name);
            messageActivity?.SetTag("outbox.type", message.Type);
            messageActivity?.SetTag("outbox.publish_attempt_count", message.PublishAttemptCount);

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
                logger.LogWarning(
                    "Outbox message {OutboxMessageType} from {OutboxSource} failed during dispatch. Exhausted: {Exhausted}.",
                    message.Type,
                    source.GetType().Name,
                    exhausted);
                messageActivity?.SetTag("outbox.dispatch_failed", true);
                failedCount++;
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
            messageActivity?.SetTag("outbox.dispatch_processed", true);
        }

        foreach (var source in touchedSources)
        {
            await source.SaveChangesAsync(cancellationToken);
        }

        batchActivity?.SetTag("outbox.processed_message_count", processedCount);
        batchActivity?.SetTag("outbox.failed_message_count", failedCount);
        RecordBatchMetrics(pendingMessages.Count, processedCount, failedCount, stopwatch.Elapsed);
        logger.LogInformation(
            "Completed outbox dispatch batch. Pending: {PendingMessageCount}. Processed: {ProcessedMessageCount}. Failed: {FailedMessageCount}. DurationMs: {DurationMs}.",
            pendingMessages.Count,
            processedCount,
            failedCount,
            stopwatch.Elapsed.TotalMilliseconds);

        return processedCount;
    }

    private static void RecordBatchMetrics(
        int pendingCount,
        int processedCount,
        int failedCount,
        TimeSpan elapsed)
    {
        OutboxDispatcherDiagnostics.Batches.Add(1);
        OutboxDispatcherDiagnostics.PendingMessages.Add(pendingCount);
        OutboxDispatcherDiagnostics.ProcessedMessages.Add(processedCount);
        OutboxDispatcherDiagnostics.FailedMessages.Add(failedCount);
        OutboxDispatcherDiagnostics.DurationMs.Record(elapsed.TotalMilliseconds);
    }
}
