using LIAnsureProtect.Modules.Notifications.Application;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Realtime;

internal sealed class NoOpNotificationRealtimePublisher : INotificationRealtimePublisher
{
    public Task PublishChangedAsync(
        NotificationMessage message,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
