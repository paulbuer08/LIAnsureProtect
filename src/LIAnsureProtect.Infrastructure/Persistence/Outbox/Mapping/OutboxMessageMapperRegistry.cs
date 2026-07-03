using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping;

public sealed class OutboxMessageMapperRegistry<TOutput>(IEnumerable<IOutboxMessageMapper<TOutput>> mappers)
{
    private readonly Dictionary<string, IOutboxMessageMapper<TOutput>> mappersByType = mappers
        .GroupBy(mapper => mapper.EventType, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

    public bool TryMap(IOutboxMessageView outboxMessage, out TOutput? mapped)
    {
        if (!mappersByType.TryGetValue(outboxMessage.Type, out var mapper))
        {
            mapped = default;
            return false;
        }

        mapped = mapper.Map(outboxMessage);
        return true;
    }
}
