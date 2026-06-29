using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Notifications.Application.Queries.ListMyNotifications;

public sealed class ListMyNotificationsQueryHandler(
    INotificationInboxRepository notificationInboxRepository,
    ITeamNotificationRepository teamNotificationRepository,
    ICurrentUser currentUser)
    : IRequestHandler<ListMyNotificationsQuery, ListMyNotificationsResult>
{
    public async Task<ListMyNotificationsResult> Handle(
        ListMyNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var recipientUserId = GetRequiredCurrentUserId();
        var teamAudiences = NotificationTeamAudiences.ForRoles(currentUser.GetRoles());

        var personal = await notificationInboxRepository.ListForRecipientAsync(
            recipientUserId,
            cancellationToken);
        var personalUnread = await notificationInboxRepository.CountUnreadForRecipientAsync(
            recipientUserId,
            cancellationToken);

        IReadOnlyList<NotificationInboxItemResult> team = [];
        var teamUnread = 0;
        if (teamAudiences.Count > 0)
        {
            team = await teamNotificationRepository.ListForAudiencesAsync(
                recipientUserId,
                teamAudiences,
                cancellationToken);
            teamUnread = await teamNotificationRepository.CountUnreadForAudiencesAsync(
                recipientUserId,
                teamAudiences,
                cancellationToken);
        }

        var merged = personal
            .Concat(team)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ToList();

        return new ListMyNotificationsResult(merged, personalUnread + teamUnread);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to list notifications.")
            : currentUser.UserId;
    }
}
