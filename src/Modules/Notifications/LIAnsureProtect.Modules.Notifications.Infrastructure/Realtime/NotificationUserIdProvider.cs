using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Realtime;

internal sealed class NotificationUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? connection.User.FindFirstValue("sub");
}
