namespace LIAnsureProtect.Application.Notifications;

public interface INotificationPublisher
{
    Task<NotificationPublishResult> PublishAsync(
        NotificationMessage message,
        CancellationToken cancellationToken);
}
