using MediatR;

namespace LIAnsureProtect.Application.Notifications.Queries.ListMyNotifications;

public sealed record ListMyNotificationsQuery : IRequest<ListMyNotificationsResult>;
