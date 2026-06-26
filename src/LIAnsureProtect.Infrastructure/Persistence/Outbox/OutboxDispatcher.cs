using LIAnsureProtect.Application.Notifications;
using LIAnsureProtect.Infrastructure.Persistence.Notifications;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

public sealed class OutboxDispatcher(
    SubmissionDbContext dbContext,
    INotificationPublisher notificationPublisher) : IOutboxDispatcher
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
            var notificationMessage = OutboxNotificationMapper.TryMap(message);
            if (notificationMessage is null)
            {
                message.MarkProcessed(nowUtc);
                processedCount++;
                continue;
            }

            // Drop a copy in the recipient's inbox (read model) before publishing.
            await EnsureInboxEntryAsync(message, notificationMessage, nowUtc, cancellationToken);

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

    // Persist a per-recipient inbox entry for person-addressed notifications.
    // Idempotent on the source outbox message id so dispatcher retries never duplicate.
    private async Task EnsureInboxEntryAsync(
        OutboxMessage message,
        NotificationMessage notificationMessage,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
    {
        if (notificationMessage.Audience != NotificationAudiences.CustomerOrBroker)
            return;

        if (string.IsNullOrWhiteSpace(notificationMessage.OwnerUserId))
            return;

        var alreadyExists = await dbContext.Set<NotificationInboxEntry>()
            .AnyAsync(entry => entry.SourceOutboxMessageId == message.Id, cancellationToken);
        if (alreadyExists)
            return;

        await dbContext.Set<NotificationInboxEntry>().AddAsync(
            NotificationInboxEntry.FromNotificationMessage(notificationMessage, createdAtUtc),
            cancellationToken);
    }
}
