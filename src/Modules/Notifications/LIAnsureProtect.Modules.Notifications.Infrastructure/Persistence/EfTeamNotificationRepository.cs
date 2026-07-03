using System.Text.Json;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Application.Queries.ListMyNotifications;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;

public sealed class EfTeamNotificationRepository(NotificationsDbContext dbContext) : ITeamNotificationRepository
{
    private const int MaxListSize = 50;

    public async Task<IReadOnlyList<NotificationInboxItemResult>> ListForAudiencesAsync(
        string recipientUserId,
        IReadOnlyCollection<string> audiences,
        CancellationToken cancellationToken)
    {
        if (audiences.Count == 0)
            return [];

        var entries = await dbContext.TeamNotificationEntries
            .AsNoTracking()
            .Where(entry => audiences.Contains(entry.Audience))
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Take(MaxListSize)
            .Select(entry => new
            {
                entry.Id,
                entry.Audience,
                entry.Type,
                entry.SubjectReferenceType,
                entry.SubjectReferenceId,
                entry.AttributesJson,
                entry.OccurredAtUtc,
                // Per-user read state: this caller's receipt, if any.
                ReadAtUtc = entry.ReadReceipts
                    .Where(receipt => receipt.RecipientUserId == recipientUserId)
                    .Select(receipt => (DateTime?)receipt.ReadAtUtc)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return entries
            .Select(entry => new NotificationInboxItemResult(
                entry.Id,
                NotificationScopes.Team,
                entry.Audience,
                entry.Type,
                NotificationInboxTitles.For(entry.Type),
                entry.SubjectReferenceType,
                entry.SubjectReferenceId,
                DeserializeAttributes(entry.AttributesJson),
                entry.OccurredAtUtc,
                entry.ReadAtUtc is not null,
                entry.ReadAtUtc))
            .ToList();
    }

    public Task<int> CountUnreadForAudiencesAsync(
        string recipientUserId,
        IReadOnlyCollection<string> audiences,
        CancellationToken cancellationToken)
    {
        if (audiences.Count == 0)
            return Task.FromResult(0);

        return dbContext.TeamNotificationEntries
            .CountAsync(
                entry => audiences.Contains(entry.Audience)
                    && !entry.ReadReceipts.Any(receipt => receipt.RecipientUserId == recipientUserId),
                cancellationToken);
    }

    public async Task<bool> MarkReadAsync(
        Guid teamNotificationEntryId,
        string recipientUserId,
        IReadOnlyCollection<string> allowedAudiences,
        DateTime readAtUtc,
        CancellationToken cancellationToken)
    {
        var entry = await dbContext.TeamNotificationEntries
            .Include(candidate => candidate.ReadReceipts)
            .FirstOrDefaultAsync(candidate => candidate.Id == teamNotificationEntryId, cancellationToken);

        // Not found, or the caller's role does not grant this entry's audience.
        if (entry is null || !allowedAudiences.Contains(entry.Audience))
            return false;

        entry.MarkReadBy(recipientUserId, readAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static Dictionary<string, string> DeserializeAttributes(string attributesJson)
    {
        if (string.IsNullOrWhiteSpace(attributesJson))
            return new Dictionary<string, string>();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(attributesJson)
            ?? new Dictionary<string, string>();
    }
}
