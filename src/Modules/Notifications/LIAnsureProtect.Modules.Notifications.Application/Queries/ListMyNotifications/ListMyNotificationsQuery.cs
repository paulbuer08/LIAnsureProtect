using MediatR;

namespace LIAnsureProtect.Modules.Notifications.Application.Queries.ListMyNotifications;

public sealed record ListMyNotificationsQuery(
    string? Search = null,
    string? Type = null,
    bool? IsUnread = null,
    string? Scope = null) : IRequest<ListMyNotificationsResult>;

public sealed record NotificationListFilter(
    string? Search,
    string? Type,
    bool? IsUnread);
