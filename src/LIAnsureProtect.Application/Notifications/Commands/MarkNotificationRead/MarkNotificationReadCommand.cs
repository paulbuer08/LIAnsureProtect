using MediatR;

namespace LIAnsureProtect.Application.Notifications.Commands.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest<bool>;
