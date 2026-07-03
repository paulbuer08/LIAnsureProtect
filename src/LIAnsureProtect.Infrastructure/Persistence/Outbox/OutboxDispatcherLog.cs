using Microsoft.Extensions.Logging;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

/// <summary>
/// Source-generated high-performance log messages for the outbox dispatcher (CA1848): no boxing,
/// no format parsing, and arguments are not evaluated when the level is disabled.
/// </summary>
internal static partial class OutboxDispatcherLog
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Dispatching {PendingMessageCount} outbox messages from {SourceCount} sources.")]
    public static partial void DispatchingBatch(ILogger logger, int pendingMessageCount, int sourceCount);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Outbox message consumer {OutboxConsumer} threw while processing {OutboxMessageType}.")]
    public static partial void ConsumerThrew(ILogger logger, Exception exception, string outboxConsumer, string outboxMessageType);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Outbox message {OutboxMessageType} from {OutboxSource} failed during dispatch. Exhausted: {Exhausted}.")]
    public static partial void MessageFailed(ILogger logger, string outboxMessageType, string outboxSource, bool exhausted);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to save outbox dispatch state for {OutboxSource}. Its messages will be re-delivered.")]
    public static partial void SaveFailed(ILogger logger, Exception exception, string outboxSource);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Completed outbox dispatch batch. Pending: {PendingMessageCount}. Processed: {ProcessedMessageCount}. Failed: {FailedMessageCount}. DurationMs: {DurationMs}.")]
    public static partial void BatchCompleted(ILogger logger, int pendingMessageCount, int processedMessageCount, int failedMessageCount, double durationMs);
}
