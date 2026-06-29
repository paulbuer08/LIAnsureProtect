using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Notifications.Application.Queries.ListMyNotifications;

public sealed class ListMyNotificationsQueryHandler(
    INotificationInboxRepository notificationInboxRepository,
    ICurrentUser currentUser)
    : IRequestHandler<ListMyNotificationsQuery, ListMyNotificationsResult>
{
    public async Task<ListMyNotificationsResult> Handle(
        ListMyNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var recipientUserId = GetRequiredCurrentUserId();

        var notifications = await notificationInboxRepository.ListForRecipientAsync(
            recipientUserId,
            cancellationToken);

        var unreadCount = await notificationInboxRepository.CountUnreadForRecipientAsync(
            recipientUserId,
            cancellationToken);

        return new ListMyNotificationsResult(notifications, unreadCount);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to list notifications.")
            : currentUser.UserId;
    }
}
