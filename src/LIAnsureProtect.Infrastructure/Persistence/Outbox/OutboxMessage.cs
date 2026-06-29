using System.Text.Json;
using LIAnsureProtect.Platform.Abstractions.DomainEvents;

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

    public int PublishAttemptCount { get; private set; }

    public DateTime? LastPublishAttemptAtUtc { get; private set; }

    public DateTime? NextAttemptAtUtc { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public DateTime? FailedAtUtc { get; private set; }

    public void MarkProcessed(DateTime processedAtUtc)
    {
        ProcessedAtUtc = processedAtUtc;
        NextAttemptAtUtc = null;
        FailedAtUtc = null;
        Error = null;
    }

    public void MarkPublishSucceeded(DateTime processedAtUtc, string providerMessageId)
    {
        if (string.IsNullOrWhiteSpace(providerMessageId))
            throw new ArgumentException("Provider message id is required.", nameof(providerMessageId));

        PublishAttemptCount++;
        LastPublishAttemptAtUtc = processedAtUtc;
        ProviderMessageId = providerMessageId.Trim();
        MarkProcessed(processedAtUtc);
    }

    public void MarkPublishFailed(
        DateTime attemptedAtUtc,
        string failureReason,
        DateTime? nextAttemptAtUtc,
        bool exhausted)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("Failure reason is required.", nameof(failureReason));

        PublishAttemptCount++;
        LastPublishAttemptAtUtc = attemptedAtUtc;
        NextAttemptAtUtc = exhausted ? null : nextAttemptAtUtc;
        FailedAtUtc = exhausted ? attemptedAtUtc : null;
        Error = failureReason.Trim();
    }

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
