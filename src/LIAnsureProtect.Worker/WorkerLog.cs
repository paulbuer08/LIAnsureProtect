namespace LIAnsureProtect.Worker;

/// <summary>
/// Source-generated high-performance log messages for the worker poll loop (CA1848): no boxing,
/// no format parsing, and arguments are not evaluated when the level is disabled.
/// </summary>
internal static partial class WorkerLog
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Processed {ProcessedOutboxMessageCount} outbox message(s).")]
    public static partial void ProcessedOutboxMessages(ILogger logger, int processedOutboxMessageCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Deleted {DeletedIdempotencyRecordCount} expired completed idempotency record(s).")]
    public static partial void DeletedIdempotencyRecords(ILogger logger, int deletedIdempotencyRecordCount);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Outbox worker poll iteration failed. Retrying on the next poll.")]
    public static partial void PollIterationFailed(ILogger logger, Exception exception);
}
