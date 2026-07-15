namespace LIAnsureProtect.Modules.Notifications.Application.Queries.ListMyNotifications;

public sealed record ListMyNotificationsResult(
    IReadOnlyList<NotificationInboxItemResult> Notifications,
    int UnreadCount);

public sealed record NotificationInboxItemResult(
    Guid NotificationId,
    string Scope,
    string Audience,
    string Type,
    string Title,
    string SubjectReferenceType,
    string SubjectReferenceId,
    IReadOnlyDictionary<string, string> Attributes,
    DateTime OccurredAtUtc,
    bool IsRead,
    DateTime? ReadAtUtc,
    string LifecycleState = "Active",
    DateTime? HistoricalAtUtc = null,
    string? HistoricalReason = null,
    Guid? ReplacementQuoteId = null,
    int? ReplacementQuoteVersion = null);
