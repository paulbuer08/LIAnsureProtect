namespace LIAnsureProtect.Platform.Abstractions.Outbox;

/// <summary>
/// A source-neutral view of one pending outbox row the dispatcher can map, publish, and mark.
/// </summary>
public interface IOutboxMessageView
{
    Guid Id { get; }
    string Type { get; }
    string Payload { get; }
    DateTime CreatedAtUtc { get; }
    int PublishAttemptCount { get; }

    void MarkProcessed(DateTime processedAtUtc);
    void MarkPublishSucceeded(DateTime processedAtUtc, string providerMessageId);
    void MarkPublishFailed(DateTime attemptedAtUtc, string failureReason, DateTime? nextAttemptAtUtc, bool exhausted);
}
