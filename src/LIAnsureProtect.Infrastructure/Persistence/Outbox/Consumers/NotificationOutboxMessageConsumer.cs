using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using Microsoft.Extensions.Logging;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Consumers;

public sealed class NotificationOutboxMessageConsumer(
    OutboxMessageMapperRegistry<NotificationMessage> registry,
    INotificationProjector notificationProjector,
    INotificationPublisher notificationPublisher,
    INotificationRealtimePublisher notificationRealtimePublisher,
    ILogger<NotificationOutboxMessageConsumer> logger) : IOutboxMessageConsumer
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

        try
        {
            await notificationRealtimePublisher.PublishChangedAsync(
                notificationMessage,
                cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Realtime delivery is an advisory cache-invalidation hint. The committed inbox is the
            // source of truth, so Redis or browser-channel failure must never poison the outbox row.
            RealtimeInvalidationFailed(logger, outboxMessage.Id, exception);
        }

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

    private static readonly Action<ILogger, Guid, Exception?> RealtimeInvalidationFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(4101, nameof(RealtimeInvalidationFailed)),
            "Notification realtime invalidation failed for outbox message {OutboxMessageId}.");
}
