using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;

public sealed class EfNotificationSubjectAcknowledgementRepository(NotificationsDbContext dbContext)
    : INotificationSubjectAcknowledgementRepository
{
    public async Task AcknowledgeAsync(
        string recipientUserId,
        string scope,
        IReadOnlyCollection<string> audiences,
        string subjectReferenceType,
        string subjectReferenceId,
        DateTime viewedThroughUtc,
        CancellationToken cancellationToken)
    {
        foreach (var audience in audiences)
        {
            var view = await dbContext.NotificationSubjectViews.FirstOrDefaultAsync(
                candidate => candidate.RecipientUserId == recipientUserId
                    && candidate.Scope == scope
                    && candidate.Audience == audience
                    && candidate.SubjectReferenceType == subjectReferenceType
                    && candidate.SubjectReferenceId == subjectReferenceId,
                cancellationToken);

            if (view is null)
            {
                await dbContext.NotificationSubjectViews.AddAsync(
                    NotificationSubjectView.Create(
                        recipientUserId,
                        scope,
                        audience,
                        subjectReferenceType,
                        subjectReferenceId,
                        viewedThroughUtc),
                    cancellationToken);
            }
            else
            {
                view.MoveThrough(viewedThroughUtc);
            }
        }

        if (scope == NotificationScopes.Personal)
        {
            var entries = await dbContext.NotificationInboxEntries
                .Where(entry => entry.RecipientUserId == recipientUserId
                    && entry.SubjectReferenceType == subjectReferenceType
                    && entry.SubjectReferenceId == subjectReferenceId
                    && entry.OccurredAtUtc <= viewedThroughUtc
                    && entry.LifecycleState == NotificationLifecycleState.Active)
                .ToListAsync(cancellationToken);
            foreach (var entry in entries)
                entry.MarkRead(viewedThroughUtc);
        }
        else
        {
            var entries = await dbContext.TeamNotificationEntries
                .Include(entry => entry.ReadReceipts)
                .Where(entry => audiences.Contains(entry.Audience)
                    && entry.SubjectReferenceType == subjectReferenceType
                    && entry.SubjectReferenceId == subjectReferenceId
                    && entry.OccurredAtUtc <= viewedThroughUtc
                    && entry.LifecycleState == NotificationLifecycleState.Active)
                .ToListAsync(cancellationToken);
            foreach (var entry in entries)
                entry.MarkReadBy(recipientUserId, viewedThroughUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
