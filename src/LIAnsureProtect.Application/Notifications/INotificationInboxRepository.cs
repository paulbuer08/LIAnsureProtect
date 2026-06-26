using LIAnsureProtect.Application.Notifications.Queries.ListMyNotifications;

namespace LIAnsureProtect.Application.Notifications;

public interface INotificationInboxRepository
{
    Task<IReadOnlyList<NotificationInboxItemResult>> ListForRecipientAsync(
        string recipientUserId,
        CancellationToken cancellationToken);

    Task<int> CountUnreadForRecipientAsync(
        string recipientUserId,
        CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(
        Guid notificationId,
        string recipientUserId,
        DateTime readAtUtc,
        CancellationToken cancellationToken);
}
