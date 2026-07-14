namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Realtime;

internal static class NotificationRealtimeGroups
{
    public static string ForUser(string userId) => $"notification-user:{userId}";

    public static string ForTeam(string audience) => $"notification-team:{audience}";
}
