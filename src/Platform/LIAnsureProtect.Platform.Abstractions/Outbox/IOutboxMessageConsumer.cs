namespace LIAnsureProtect.Platform.Abstractions.Outbox;

public interface IOutboxMessageConsumer
{
    Task<OutboxMessageConsumerResult> ConsumeAsync(
        IOutboxMessageView outboxMessage,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
