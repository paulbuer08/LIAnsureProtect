using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

public sealed class OutboxDispatcher(
    IEnumerable<IOutboxSource> sources,
    INotificationProjector notificationProjector,
    INotificationPublisher notificationPublisher,
    IReferralOperationProjector referralOperationProjector) : IOutboxDispatcher
{
    private const int BatchSize = 20;
    private const int MaxPublishAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    public async Task<int> DispatchPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var sourceList = sources.ToList();
        var pendingMessages = new List<(IOutboxMessageView Message, IOutboxSource Source)>();

        foreach (var source in sourceList)
        {
            foreach (var message in await source.GetPendingAsync(BatchSize, nowUtc, cancellationToken))
            {
                pendingMessages.Add((message, source));
            }
        }

        if (pendingMessages.Count == 0)
            return 0;

        var orderedMessages = pendingMessages
            .OrderBy(item => item.Message.CreatedAtUtc)
            .ToList();
        var touchedSources = new HashSet<IOutboxSource>();
        var processedCount = 0;

        foreach (var (message, source) in orderedMessages)
        {
            touchedSources.Add(source);

            var referralEvent = OutboxReferralOperationMapper.TryMap(message);
            if (referralEvent is not null)
                await referralOperationProjector.ProjectAsync(referralEvent, cancellationToken);

            var notificationMessage = OutboxNotificationMapper.TryMap(message);
            if (notificationMessage is null)
            {
                message.MarkProcessed(nowUtc);
                processedCount++;
                continue;
            }

            // Project into the Notifications module's inbox (its own context/transaction, idempotent on
            // the source outbox message id) BEFORE publishing and before marking this row processed.
            // This ordering makes the cross-context handoff safe without a distributed transaction:
            // a crash anywhere just re-delivers, and the unique index dedupes the inbox entry.
            await notificationProjector.ProjectAsync(notificationMessage, cancellationToken);

            var publishResult = await notificationPublisher.PublishAsync(
                notificationMessage,
                cancellationToken);

            if (publishResult.IsSuccess)
            {
                message.MarkPublishSucceeded(
                    nowUtc,
                    publishResult.ProviderMessageId ?? string.Empty);
                processedCount++;
                continue;
            }

            var nextAttemptNumber = message.PublishAttemptCount + 1;
            var exhausted = !publishResult.IsTransient || nextAttemptNumber >= MaxPublishAttempts;
            message.MarkPublishFailed(
                nowUtc,
                publishResult.FailureReason ?? "Notification publish failed.",
                exhausted ? null : nowUtc.Add(RetryDelay),
                exhausted);
        }

        foreach (var source in touchedSources)
        {
            await source.SaveChangesAsync(cancellationToken);
        }

        return processedCount;
    }
}
