namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Realtime;

public interface INotificationRealtimeClient
{
    Task NotificationsChanged();
}
