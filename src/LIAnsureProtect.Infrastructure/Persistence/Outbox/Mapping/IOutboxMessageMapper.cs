using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping;

public interface IOutboxMessageMapper<out TOutput>
{
    string EventType { get; }

    TOutput? Map(IOutboxMessageView outboxMessage);
}
