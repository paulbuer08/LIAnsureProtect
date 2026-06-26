using System.Text.Json;
using LIAnsureProtect.Application.Notifications;
using LIAnsureProtect.Application.Notifications.Queries.ListMyNotifications;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Persistence.Notifications;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Notifications;

public sealed class EfNotificationInboxRepository(SubmissionDbContext dbContext) : INotificationInboxRepository
{
    private const int MaxListSize = 50;

    public async Task<IReadOnlyList<NotificationInboxItemResult>> ListForRecipientAsync(
        string recipientUserId,
        CancellationToken cancellationToken)
    {
        var entries = await dbContext.Set<NotificationInboxEntry>()
            .AsNoTracking()
            .Where(entry => entry.RecipientUserId == recipientUserId)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Take(MaxListSize)
            .Select(entry => new
            {
                entry.Id,
                entry.Type,
                entry.SubjectReferenceType,
                entry.SubjectReferenceId,
                entry.AttributesJson,
                entry.OccurredAtUtc,
                entry.ReadAtUtc
            })
            .ToListAsync(cancellationToken);

        return entries
            .Select(entry => new NotificationInboxItemResult(
                entry.Id,
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

    public Task<int> CountUnreadForRecipientAsync(
        string recipientUserId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<NotificationInboxEntry>()
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
        var entry = await dbContext.Set<NotificationInboxEntry>()
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

    private static IReadOnlyDictionary<string, string> DeserializeAttributes(string attributesJson)
    {
        if (string.IsNullOrWhiteSpace(attributesJson))
            return new Dictionary<string, string>();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(attributesJson)
            ?? new Dictionary<string, string>();
    }
}
