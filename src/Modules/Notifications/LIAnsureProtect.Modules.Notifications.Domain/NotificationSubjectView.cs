namespace LIAnsureProtect.Modules.Notifications.Domain;

/// <summary>
/// Recipient-scoped watermark recording that an exact notification subject was viewed through a
/// particular UTC instant. The projector uses it to prevent an older, delayed notification from
/// becoming unread after the subject has already been opened.
/// </summary>
public sealed class NotificationSubjectView
{
    private NotificationSubjectView()
    {
        RecipientUserId = string.Empty;
        Scope = string.Empty;
        Audience = string.Empty;
        SubjectReferenceType = string.Empty;
        SubjectReferenceId = string.Empty;
    }

    public Guid Id { get; private set; }
    public string RecipientUserId { get; private set; }
    public string Scope { get; private set; }
    public string Audience { get; private set; }
    public string SubjectReferenceType { get; private set; }
    public string SubjectReferenceId { get; private set; }
    public DateTime ViewedThroughUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static NotificationSubjectView Create(
        string recipientUserId,
        string scope,
        string audience,
        string subjectReferenceType,
        string subjectReferenceId,
        DateTime viewedThroughUtc)
    {
        return new NotificationSubjectView
        {
            Id = Guid.NewGuid(),
            RecipientUserId = Require(recipientUserId, nameof(recipientUserId)),
            Scope = Require(scope, nameof(scope)),
            Audience = Require(audience, nameof(audience)),
            SubjectReferenceType = Require(subjectReferenceType, nameof(subjectReferenceType)),
            SubjectReferenceId = Require(subjectReferenceId, nameof(subjectReferenceId)),
            ViewedThroughUtc = viewedThroughUtc,
            CreatedAtUtc = viewedThroughUtc,
            UpdatedAtUtc = viewedThroughUtc
        };
    }

    public void MoveThrough(DateTime viewedThroughUtc)
    {
        if (viewedThroughUtc <= ViewedThroughUtc)
            return;

        ViewedThroughUtc = viewedThroughUtc;
        UpdatedAtUtc = viewedThroughUtc;
    }

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A notification subject-view value is required.", parameterName);

        return value.Trim();
    }
}
