using System.Text.Json;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Application.Queries.ListMyNotifications;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;

public sealed class EfNotificationInboxRepository(NotificationsDbContext dbContext) : INotificationInboxRepository
{
    private const int MaxListSize = 50;

    public async Task<IReadOnlyList<NotificationInboxItemResult>> ListForRecipientAsync(
        string recipientUserId,
        CancellationToken cancellationToken,
        NotificationListFilter? filter = null)
    {
        filter ??= new NotificationListFilter(null, null, null);
        var query = dbContext.NotificationInboxEntries
            .AsNoTracking()
            .Where(entry => entry.RecipientUserId == recipientUserId);

        if (!string.IsNullOrWhiteSpace(filter.Type))
            query = query.Where(entry => entry.Type == filter.Type);
        if (filter.IsUnread is not null)
            query = filter.IsUnread.Value
                ? query.Where(entry => entry.ReadAtUtc == null)
                : query.Where(entry => entry.ReadAtUtc != null);

        var projectedQuery = query
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Select(entry => new
            {
                entry.Id,
                entry.Audience,
                entry.Type,
                entry.SubjectReferenceType,
                entry.SubjectReferenceId,
                entry.AttributesJson,
                entry.OccurredAtUtc,
                entry.ReadAtUtc
            });

        var entries = string.IsNullOrWhiteSpace(filter.Search)
            ? await projectedQuery.Take(MaxListSize).ToListAsync(cancellationToken)
            : (await projectedQuery.ToListAsync(cancellationToken))
                .Where(entry => MatchesSearch(
                    filter.Search,
                    entry.Type,
                    entry.SubjectReferenceType,
                    entry.SubjectReferenceId,
                    entry.AttributesJson))
                .Take(MaxListSize)
                .ToList();

        return entries
            .Select(entry =>
            {
                var attributes = DeserializeAttributes(entry.AttributesJson);
                return new NotificationInboxItemResult(
                entry.Id,
                NotificationScopes.Personal,
                entry.Audience,
                entry.Type,
                NotificationInboxTitles.For(entry.Type, attributes),
                entry.SubjectReferenceType,
                entry.SubjectReferenceId,
                attributes,
                entry.OccurredAtUtc,
                entry.ReadAtUtc is not null,
                entry.ReadAtUtc);
            })
            .ToList();
    }

    public Task<int> CountUnreadForRecipientAsync(
        string recipientUserId,
        CancellationToken cancellationToken)
    {
        return dbContext.NotificationInboxEntries
            .CountAsync(
                entry => entry.RecipientUserId == recipientUserId && entry.ReadAtUtc == null,
                cancellationToken);
    }

    public async Task<bool> MarkReadAsync(
        Guid notificationId,
        string recipientUserId,
        DateTime readAtUtc,
        CancellationToken cancellationToken)
    {
        var entry = await dbContext.NotificationInboxEntries
            .FirstOrDefaultAsync(
                candidate => candidate.Id == notificationId && candidate.RecipientUserId == recipientUserId,
                cancellationToken);

        if (entry is null)
            return false;

        if (entry.ReadAtUtc is null)
        {
            entry.MarkRead(readAtUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static Dictionary<string, string> DeserializeAttributes(string attributesJson)
    {
        if (string.IsNullOrWhiteSpace(attributesJson))
            return new Dictionary<string, string>();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(attributesJson)
            ?? new Dictionary<string, string>();
    }

    private static bool MatchesSearch(string search, params string[] values)
    {
        var trimmed = search.Trim();
        return values.Any(value => value.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
