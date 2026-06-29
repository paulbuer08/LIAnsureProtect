using MediatR;

namespace LIAnsureProtect.Modules.Notifications.Application.Queries.ListMyNotifications;

public sealed record ListMyNotificationsQuery : IRequest<ListMyNotificationsResult>;
