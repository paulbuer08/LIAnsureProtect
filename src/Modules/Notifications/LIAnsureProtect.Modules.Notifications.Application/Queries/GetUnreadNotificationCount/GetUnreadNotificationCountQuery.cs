using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Notifications.Application.Queries.GetUnreadNotificationCount;

public sealed record GetUnreadNotificationCountQuery : IRequest<GetUnreadNotificationCountResult>;

public sealed record GetUnreadNotificationCountResult(int UnreadCount);

public sealed class GetUnreadNotificationCountQueryHandler(
    INotificationInboxRepository notificationInboxRepository,
    ITeamNotificationRepository teamNotificationRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetUnreadNotificationCountQuery, GetUnreadNotificationCountResult>
{
    public async Task<GetUnreadNotificationCountResult> Handle(
        GetUnreadNotificationCountQuery request,
        CancellationToken cancellationToken)
    {
        var recipientUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to count notifications.")
            : currentUser.UserId;

        var personalUnread = await notificationInboxRepository.CountUnreadForRecipientAsync(
            recipientUserId,
            cancellationToken);
        var teamAudiences = NotificationTeamAudiences.ForRoles(currentUser.GetRoles());
        var teamUnread = teamAudiences.Count == 0
            ? 0
            : await teamNotificationRepository.CountUnreadForAudiencesAsync(
                recipientUserId,
                teamAudiences,
                cancellationToken);

        return new GetUnreadNotificationCountResult(personalUnread + teamUnread);
    }
}
