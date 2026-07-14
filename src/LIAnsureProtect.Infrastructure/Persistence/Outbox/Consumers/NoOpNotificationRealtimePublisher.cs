using LIAnsureProtect.Modules.Notifications.Application;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Consumers;

internal sealed class NoOpNotificationRealtimePublisher : INotificationRealtimePublisher
{
    public Task PublishChangedAsync(
        NotificationMessage message,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
