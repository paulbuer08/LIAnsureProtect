using LIAnsureProtect.Modules.Notifications.Application.Queries.ListMyNotifications;

namespace LIAnsureProtect.Modules.Notifications.Application;

public interface INotificationInboxRepository
{
    Task<IReadOnlyList<NotificationInboxItemResult>> ListForRecipientAsync(
        string recipientUserId,
        CancellationToken cancellationToken,
        NotificationListFilter? filter = null);

    Task<int> CountUnreadForRecipientAsync(
        string recipientUserId,
        CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(
        Guid notificationId,
        string recipientUserId,
        DateTime readAtUtc,
        CancellationToken cancellationToken);
}
