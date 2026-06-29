using System.Text.Json;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// Projects a published notification into the inbox read model. Person-addressed
/// (customer-or-broker) notifications get a per-recipient entry; team audiences
/// (underwriting-operations, binding-operations) get a single shared team entry. Both are idempotent
/// on the source outbox message id, so the dispatcher's at-least-once delivery never duplicates.
/// </summary>
public sealed class NotificationInboxProjector(NotificationsDbContext dbContext) : INotificationProjector
{
    public async Task ProjectAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        switch (message.Audience)
        {
            case NotificationAudiences.CustomerOrBroker:
                await ProjectPersonalAsync(message, cancellationToken);
                break;
            case NotificationAudiences.UnderwritingOperations:
            case NotificationAudiences.BindingOperations:
                await ProjectTeamAsync(message, cancellationToken);
                break;
            default:
                // Unknown audience: nothing to persist.
                break;
        }
    }

    private async Task ProjectPersonalAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
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

    private async Task ProjectTeamAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        var alreadyExists = await dbContext.TeamNotificationEntries
            .AnyAsync(entry => entry.SourceOutboxMessageId == message.OutboxMessageId, cancellationToken);
        if (alreadyExists)
            return;

        var entry = TeamNotificationEntry.Create(
            message.Audience,
            message.Type,
            message.SubjectReferenceType,
            message.SubjectReferenceId,
            JsonSerializer.Serialize(message.Attributes),
            message.OutboxMessageId,
            message.OccurredAtUtc,
            DateTime.UtcNow);

        await dbContext.TeamNotificationEntries.AddAsync(entry, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
