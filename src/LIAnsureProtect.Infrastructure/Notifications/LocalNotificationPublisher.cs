using LIAnsureProtect.Application.Notifications;

namespace LIAnsureProtect.Infrastructure.Notifications;

public sealed class LocalNotificationPublisher : INotificationPublisher
{
    public Task<NotificationPublishResult> PublishAsync(
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var providerMessageId = $"local-notification-{message.MessageId}";

        return Task.FromResult(NotificationPublishResult.Success(providerMessageId));
    }
}
