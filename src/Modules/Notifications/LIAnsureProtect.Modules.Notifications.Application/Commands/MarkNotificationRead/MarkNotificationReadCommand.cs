using MediatR;

namespace LIAnsureProtect.Modules.Notifications.Application.Commands.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest<bool>;
