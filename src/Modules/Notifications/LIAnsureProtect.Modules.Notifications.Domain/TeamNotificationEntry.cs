namespace LIAnsureProtect.Modules.Notifications.Domain;

/// <summary>
/// A team-addressed notification (e.g. <c>underwriting-operations</c>). Unlike the per-recipient
/// <see cref="NotificationInboxEntry"/>, a single entry is shared by everyone in the audience, and
/// each member's read state is tracked by an independent <see cref="TeamNotificationReadReceipt"/>.
/// </summary>
public sealed class TeamNotificationEntry
{
    private readonly List<TeamNotificationReadReceipt> _readReceipts = [];

    // The only constructor: EF Core materializes through it, and the Create factory assigns
    // state via the private property setters — no parameter-heavy constructor to maintain.
    private TeamNotificationEntry()
    {
        Audience = string.Empty;
        Type = string.Empty;
        SubjectReferenceType = string.Empty;
        SubjectReferenceId = string.Empty;
        AttributesJson = "{}";
    }

    public Guid Id { get; private set; }

    public string Audience { get; private set; }

    public string Type { get; private set; }

    public string SubjectReferenceType { get; private set; }

    public string SubjectReferenceId { get; private set; }

    public string AttributesJson { get; private set; }

    public Guid SourceOutboxMessageId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<TeamNotificationReadReceipt> ReadReceipts => _readReceipts;

    /// <summary>
    /// Records that <paramref name="recipientUserId"/> has read this notification. Idempotent: a second
    /// call for the same user keeps the original receipt, and other members stay unread.
    /// </summary>
    public void MarkReadBy(string recipientUserId, DateTime readAtUtc)
    {
        if (_readReceipts.Any(receipt => receipt.RecipientUserId == recipientUserId))
            return;

        _readReceipts.Add(TeamNotificationReadReceipt.Create(Id, recipientUserId, readAtUtc));
    }

    public static TeamNotificationEntry Create(
        string audience,
        string type,
        string subjectReferenceType,
        string subjectReferenceId,
        string attributesJson,
        Guid sourceOutboxMessageId,
        DateTime occurredAtUtc,
        DateTime createdAtUtc)
    {
        return new TeamNotificationEntry
        {
            Id = Guid.NewGuid(),
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
