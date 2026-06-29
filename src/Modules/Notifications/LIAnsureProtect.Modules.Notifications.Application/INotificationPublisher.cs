namespace LIAnsureProtect.Modules.Notifications.Application;

/// <summary>
/// Outbound port for delivering a notification to a provider (local now, SNS/SES later).
/// </summary>
public interface INotificationPublisher
{
    Task<NotificationPublishResult> PublishAsync(
        NotificationMessage message,
        CancellationToken cancellationToken);
}
