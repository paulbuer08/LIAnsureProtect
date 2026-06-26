using LIAnsureProtect.Application.Common.Security;
using MediatR;

namespace LIAnsureProtect.Application.Notifications.Commands.MarkNotificationRead;

public sealed class MarkNotificationReadCommandHandler(
    INotificationInboxRepository notificationInboxRepository,
    ICurrentUser currentUser)
    : IRequestHandler<MarkNotificationReadCommand, bool>
{
    public Task<bool> Handle(
        MarkNotificationReadCommand request,
        CancellationToken cancellationToken)
    {
        return notificationInboxRepository.MarkReadAsync(
            request.NotificationId,
            GetRequiredCurrentUserId(),
            DateTime.UtcNow,
            cancellationToken);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to update notifications.")
            : currentUser.UserId;
    }
}
