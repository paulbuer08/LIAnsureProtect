using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Notifications.Application.Commands.MarkNotificationRead;

public sealed class MarkNotificationReadCommandHandler(
    INotificationInboxRepository notificationInboxRepository,
    ITeamNotificationRepository teamNotificationRepository,
    ICurrentUser currentUser)
    : IRequestHandler<MarkNotificationReadCommand, bool>
{
    public async Task<bool> Handle(
        MarkNotificationReadCommand request,
        CancellationToken cancellationToken)
    {
        var recipientUserId = GetRequiredCurrentUserId();
        var readAtUtc = DateTime.UtcNow;

        // The id may be a personal inbox entry or a team entry. Try personal first; if it is not the
        // caller's personal notification, try the team inbox limited to the caller's allowed audiences.
        if (await notificationInboxRepository.MarkReadAsync(
                request.NotificationId, recipientUserId, readAtUtc, cancellationToken))
        {
            return true;
        }

        var teamAudiences = NotificationTeamAudiences.ForRoles(currentUser.GetRoles());
        if (teamAudiences.Count == 0)
            return false;

        return await teamNotificationRepository.MarkReadAsync(
            request.NotificationId, recipientUserId, teamAudiences, readAtUtc, cancellationToken);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to update notifications.")
            : currentUser.UserId;
    }
}
