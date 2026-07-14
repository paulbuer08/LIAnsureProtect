namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Realtime;

public sealed class NotificationRealtimeOptions
{
    public const string SectionName = "Notifications:Realtime";

    public bool Enabled { get; set; }

    public string RedisConnectionString { get; set; } = string.Empty;

    public string ChannelPrefix { get; set; } = "liansureprotect-notifications";
}
