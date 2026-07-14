namespace LIAnsureProtect.Modules.Notifications.Application;

/// <summary>
/// Sends a payload-free hint after a durable notification inbox projection commits.
/// Clients always re-read the authoritative inbox; this port never transports business data.
/// </summary>
public interface INotificationRealtimePublisher
{
    Task PublishChangedAsync(NotificationMessage message, CancellationToken cancellationToken);
}
