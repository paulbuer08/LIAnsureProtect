namespace LIAnsureProtect.Modules.Notifications.Application;

/// <summary>
/// Inbound port the outbox dispatcher calls to project a published notification into this module's
/// inbox read model. Implementations must be idempotent on <see cref="NotificationMessage.OutboxMessageId"/>
/// so dispatcher retries never duplicate an inbox entry.
/// </summary>
public interface INotificationProjector
{
    Task ProjectAsync(NotificationMessage message, CancellationToken cancellationToken);
}
