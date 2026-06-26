namespace LIAnsureProtect.Application.Notifications.Queries.ListMyNotifications;

public sealed record ListMyNotificationsResult(
    IReadOnlyList<NotificationInboxItemResult> Notifications,
    int UnreadCount);

public sealed record NotificationInboxItemResult(
    Guid NotificationId,
    string Type,
    string Title,
    string SubjectReferenceType,
    string SubjectReferenceId,
    IReadOnlyDictionary<string, string> Attributes,
    DateTime OccurredAtUtc,
    bool IsRead,
    DateTime? ReadAtUtc);
