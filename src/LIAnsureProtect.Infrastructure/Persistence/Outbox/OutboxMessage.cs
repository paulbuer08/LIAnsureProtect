using System.Text.Json;
using LIAnsureProtect.Domain.Common;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage(
        Guid id,
        string type,
        string payload,
        DateTime occurredAtUtc,
        DateTime createdAtUtc)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAtUtc = occurredAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    private OutboxMessage()
    {
        Type = string.Empty;
        Payload = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; }

    public string Payload { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public string? Error { get; private set; }

    public static OutboxMessage FromDomainEvent(
        IDomainEvent domainEvent,
        DateTime createdAtUtc)
    {
        var eventType = domainEvent.GetType();

        return new OutboxMessage(
            Guid.NewGuid(),
            eventType.Name,
            JsonSerializer.Serialize(domainEvent, eventType),
            domainEvent.OccurredAtUtc,
            createdAtUtc);
    }
}
