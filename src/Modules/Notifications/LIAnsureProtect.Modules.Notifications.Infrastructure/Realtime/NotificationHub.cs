using LIAnsureProtect.Modules.Notifications.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Realtime;

/// <summary>
/// Authenticated, server-to-client-only notification invalidation channel.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub<INotificationRealtimeClient>
{
    public override async Task OnConnectedAsync()
    {
        if (!string.IsNullOrWhiteSpace(Context.UserIdentifier))
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                NotificationRealtimeGroups.ForUser(Context.UserIdentifier));
        }

        var roleClaimType = (Context.User?.Identity as ClaimsIdentity)?.RoleClaimType
            ?? ClaimTypes.Role;
        var roles = Context.User?.FindAll(roleClaimType).Select(claim => claim.Value) ?? [];
        foreach (var audience in NotificationTeamAudiences.ForRoles(roles))
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                NotificationRealtimeGroups.ForTeam(audience));
        }

        await base.OnConnectedAsync();
    }
}
