using System.Text.Json;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// Projects a published notification into the inbox read model. Person-addressed
/// (customer-or-broker) notifications get a per-recipient entry; team audiences are not persisted
/// yet (the team inbox is a later milestone). Idempotent on the source outbox message id so the
/// dispatcher's at-least-once delivery never creates duplicates.
/// </summary>
public sealed class NotificationInboxProjector(NotificationsDbContext dbContext) : INotificationProjector
{
    public async Task ProjectAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        if (message.Audience != NotificationAudiences.CustomerOrBroker)
            return;

        if (string.IsNullOrWhiteSpace(message.OwnerUserId))
            return;

        var alreadyExists = await dbContext.NotificationInboxEntries
            .AnyAsync(entry => entry.SourceOutboxMessageId == message.OutboxMessageId, cancellationToken);
        if (alreadyExists)
            return;

        var entry = NotificationInboxEntry.Create(
            message.OwnerUserId,
            message.Audience,
            message.Type,
            message.SubjectReferenceType,
            message.SubjectReferenceId,
            JsonSerializer.Serialize(message.Attributes),
            message.OutboxMessageId,
            message.OccurredAtUtc,
            DateTime.UtcNow);

        await dbContext.NotificationInboxEntries.AddAsync(entry, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
