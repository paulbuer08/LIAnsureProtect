using System.Text.Json;
using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping;

internal static class OutboxMessageJson
{
    public static T Deserialize<T>(IOutboxMessageView outboxMessage)
        => JsonSerializer.Deserialize<T>(outboxMessage.Payload)
            ?? throw new InvalidOperationException(
                $"Outbox message {outboxMessage.Id} payload could not be deserialized.");
}
