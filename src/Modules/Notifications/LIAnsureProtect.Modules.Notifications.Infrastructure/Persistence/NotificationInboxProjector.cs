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
        await MarkEarlierQuoteVersionEntriesHistoricalAsync(message, cancellationToken);

        switch (message.Audience)
        {
            case NotificationAudiences.CustomerOrBroker:
                await ProjectPersonalAsync(message, cancellationToken);
                break;
            case NotificationAudiences.UnderwritingOperations:
            case NotificationAudiences.BindingOperations:
            case NotificationAudiences.ClaimsOperations:
                await ProjectTeamAsync(message, cancellationToken);
                break;
            default:
                // Unknown audience: nothing to persist.
                break;
        }
    }

    private async Task MarkEarlierQuoteVersionEntriesHistoricalAsync(
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        if (!TryGetQuoteContext(message.Attributes, out var submissionId, out var quoteId, out var quoteVersion))
            return;

        var personalEntries = await dbContext.NotificationInboxEntries
            .Where(entry => entry.RecipientUserId == message.OwnerUserId
                && entry.LifecycleState == NotificationLifecycleState.Active)
            .ToListAsync(cancellationToken);
        foreach (var entry in personalEntries)
        {
            if (IsEarlierQuoteVersion(entry.AttributesJson, submissionId, quoteVersion))
            {
                entry.MarkHistorical(
                    message.OccurredAtUtc,
                    $"Superseded by quote version {quoteVersion}.",
                    quoteId,
                    quoteVersion);
            }
        }

        var teamEntries = await dbContext.TeamNotificationEntries
            .Where(entry => entry.LifecycleState == NotificationLifecycleState.Active)
            .ToListAsync(cancellationToken);
        foreach (var entry in teamEntries)
        {
            if (IsEarlierQuoteVersion(entry.AttributesJson, submissionId, quoteVersion))
            {
                entry.MarkHistorical(
                    message.OccurredAtUtc,
                    $"Superseded by quote version {quoteVersion}.",
                    quoteId,
                    quoteVersion);
            }
        }
    }

    private static bool TryGetQuoteContext(
        IReadOnlyDictionary<string, string> attributes,
        out Guid submissionId,
        out Guid quoteId,
        out int quoteVersion)
    {
        submissionId = Guid.Empty;
        quoteId = Guid.Empty;
        quoteVersion = 0;
        var versionValue = attributes.TryGetValue("quoteVersion", out var evidenceVersion)
            ? evidenceVersion
            : attributes.GetValueOrDefault("version");

        return Guid.TryParse(attributes.GetValueOrDefault("submissionId"), out submissionId)
            && Guid.TryParse(attributes.GetValueOrDefault("quoteId"), out quoteId)
            && int.TryParse(versionValue, System.Globalization.CultureInfo.InvariantCulture, out quoteVersion)
            && quoteVersion > 1;
    }

    private static bool IsEarlierQuoteVersion(string attributesJson, Guid submissionId, int newQuoteVersion)
    {
        var attributes = JsonSerializer.Deserialize<Dictionary<string, string>>(attributesJson);
        if (attributes is null
            || !Guid.TryParse(attributes.GetValueOrDefault("submissionId"), out var existingSubmissionId)
            || existingSubmissionId != submissionId)
        {
            return false;
        }

        var versionValue = attributes.TryGetValue("quoteVersion", out var evidenceVersion)
            ? evidenceVersion
            : attributes.GetValueOrDefault("version");
        return int.TryParse(
                versionValue,
                System.Globalization.CultureInfo.InvariantCulture,
                out var existingVersion)
            && existingVersion < newQuoteVersion;
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
