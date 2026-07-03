namespace LIAnsureProtect.Modules.Notifications.Domain;

/// <summary>
/// Per-recipient inbox read model. One entry is projected from a published notification when the
/// outbox dispatcher processes a customer/broker-addressed event, so users can read the notifications
/// the system publishes. This is the Notifications module's owned aggregate.
/// </summary>
public sealed class NotificationInboxEntry
{
    // The only constructor: EF Core materializes through it, and the Create factory assigns
    // state via the private property setters — no parameter-heavy constructor to maintain.
    private NotificationInboxEntry()
    {
        RecipientUserId = string.Empty;
        Audience = string.Empty;
        Type = string.Empty;
        SubjectReferenceType = string.Empty;
        SubjectReferenceId = string.Empty;
        AttributesJson = "{}";
    }

    public Guid Id { get; private set; }

    public string RecipientUserId { get; private set; }

    public string Audience { get; private set; }

    public string Type { get; private set; }

    public string SubjectReferenceType { get; private set; }

    public string SubjectReferenceId { get; private set; }

    public string AttributesJson { get; private set; }

    public Guid SourceOutboxMessageId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    // Idempotent: keeps the original read timestamp if already read.
    public void MarkRead(DateTime readAtUtc)
    {
        ReadAtUtc ??= readAtUtc;
    }

    /// <summary>
    /// Creates a new inbox entry. Called by the projector from a published notification; the entry
    /// itself stays free of any Application/contract types so the Domain depends only on the kernel.
    /// </summary>
    public static NotificationInboxEntry Create(
        string recipientUserId,
        string audience,
        string type,
        string subjectReferenceType,
        string subjectReferenceId,
        string attributesJson,
        Guid sourceOutboxMessageId,
        DateTime occurredAtUtc,
        DateTime createdAtUtc)
    {
        return new NotificationInboxEntry
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Audience = audience,
            Type = type,
            SubjectReferenceType = subjectReferenceType,
            SubjectReferenceId = subjectReferenceId,
            AttributesJson = attributesJson,
            SourceOutboxMessageId = sourceOutboxMessageId,
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = createdAtUtc
        };
    }
}
