using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

public sealed class OutboxDispatcher(
    SubmissionDbContext dbContext,
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
        var pendingMessages = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAtUtc == null
                && message.FailedAtUtc == null
                && (message.NextAttemptAtUtc == null || message.NextAttemptAtUtc <= nowUtc))
            .OrderBy(message => message.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
            return 0;

        var processedCount = 0;

        foreach (var message in pendingMessages)
        {
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

        await dbContext.SaveChangesAsync(cancellationToken);

        return processedCount;
    }
}
