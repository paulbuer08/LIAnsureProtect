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
        var search = request.Search?.Trim();
        if (search?.Length > 200)
            throw new ArgumentException("Search text must not exceed 200 characters.", nameof(request));
        var filter = new NotificationListFilter(search, request.Type?.Trim(), request.IsUnread);
        var requestedScope = request.Scope?.Trim().ToLowerInvariant();
        var includePersonal = requestedScope != NotificationScopes.Team || teamAudiences.Count == 0;
        var includeTeam = teamAudiences.Count > 0 && requestedScope != NotificationScopes.Personal;

        var personal = includePersonal
            ? await notificationInboxRepository.ListForRecipientAsync(
                recipientUserId,
                cancellationToken,
                filter)
            : [];
        var personalUnread = await notificationInboxRepository.CountUnreadForRecipientAsync(
            recipientUserId,
            cancellationToken);

        IReadOnlyList<NotificationInboxItemResult> team = [];
        var teamUnread = 0;
        if (includeTeam)
        {
            team = await teamNotificationRepository.ListForAudiencesAsync(
                recipientUserId,
                teamAudiences,
                cancellationToken,
                filter);
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
