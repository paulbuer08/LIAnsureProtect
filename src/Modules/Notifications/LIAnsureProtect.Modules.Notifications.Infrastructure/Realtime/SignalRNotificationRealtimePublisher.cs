using LIAnsureProtect.Modules.Notifications.Application;
using Microsoft.AspNetCore.SignalR;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Realtime;

internal sealed class SignalRNotificationRealtimePublisher(
    IHubContext<NotificationHub, INotificationRealtimeClient> hubContext)
    : INotificationRealtimePublisher
{
    public Task PublishChangedAsync(
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        var clients = message.Audience == NotificationAudiences.CustomerOrBroker
            ? hubContext.Clients.Group(NotificationRealtimeGroups.ForUser(message.OwnerUserId))
            : hubContext.Clients.Group(NotificationRealtimeGroups.ForTeam(message.Audience));

        return clients.NotificationsChanged();
    }
}
