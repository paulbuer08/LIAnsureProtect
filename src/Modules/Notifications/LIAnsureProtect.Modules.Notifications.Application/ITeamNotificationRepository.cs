using LIAnsureProtect.Modules.Notifications.Application.Queries.ListMyNotifications;

namespace LIAnsureProtect.Modules.Notifications.Application;

/// <summary>
/// Reads and updates the team notification inbox. Team entries are shared by an audience; read state
/// is per-user (read receipts). All methods take the caller's allowed audiences so a caller can only
/// ever see or touch entries their role grants.
/// </summary>
public interface ITeamNotificationRepository
{
    Task<IReadOnlyList<NotificationInboxItemResult>> ListForAudiencesAsync(
        string recipientUserId,
        IReadOnlyCollection<string> audiences,
        CancellationToken cancellationToken);

    Task<int> CountUnreadForAudiencesAsync(
        string recipientUserId,
        IReadOnlyCollection<string> audiences,
        CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(
        Guid teamNotificationEntryId,
        string recipientUserId,
        IReadOnlyCollection<string> allowedAudiences,
        DateTime readAtUtc,
        CancellationToken cancellationToken);
}
