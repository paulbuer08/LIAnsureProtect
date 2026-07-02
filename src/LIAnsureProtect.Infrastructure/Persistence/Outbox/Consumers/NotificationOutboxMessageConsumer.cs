using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping;
using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Consumers;

public sealed class NotificationOutboxMessageConsumer(
    OutboxMessageMapperRegistry<NotificationMessage> registry,
    INotificationProjector notificationProjector,
    INotificationPublisher notificationPublisher) : IOutboxMessageConsumer
{
    public async Task<OutboxMessageConsumerResult> ConsumeAsync(
        IOutboxMessageView outboxMessage,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (!registry.TryMap(outboxMessage, out var notificationMessage) || notificationMessage is null)
            return OutboxMessageConsumerResult.NotHandled();

        // Project into the Notifications module's inbox before publishing and before the dispatcher
        // marks the source row processed. If publishing fails, re-delivery is safe because projection is
        // idempotent on the source outbox message id.
        await notificationProjector.ProjectAsync(notificationMessage, cancellationToken);

        var publishResult = await notificationPublisher.PublishAsync(
            notificationMessage,
            cancellationToken);

        if (publishResult.IsSuccess)
            return OutboxMessageConsumerResult.Succeeded(publishResult.ProviderMessageId ?? string.Empty);

        var failureReason = publishResult.FailureReason ?? "Notification publish failed.";
        return publishResult.IsTransient
            ? OutboxMessageConsumerResult.TransientFailure(failureReason)
            : OutboxMessageConsumerResult.PermanentFailure(failureReason);
    }
}
