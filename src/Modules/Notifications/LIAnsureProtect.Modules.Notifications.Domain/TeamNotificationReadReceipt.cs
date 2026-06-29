namespace LIAnsureProtect.Modules.Notifications.Domain;

/// <summary>
/// Records that one team member has read a <see cref="TeamNotificationEntry"/>. Team notifications are
/// shared by everyone in an audience, so read state is per-user: each member gets their own receipt,
/// created lazily the first time they mark the notification read.
/// </summary>
public sealed class TeamNotificationReadReceipt
{
    private TeamNotificationReadReceipt(
        Guid id,
        Guid teamNotificationEntryId,
        string recipientUserId,
        DateTime readAtUtc)
    {
        Id = id;
        TeamNotificationEntryId = teamNotificationEntryId;
        RecipientUserId = recipientUserId;
        ReadAtUtc = readAtUtc;
    }

    private TeamNotificationReadReceipt()
    {
        RecipientUserId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TeamNotificationEntryId { get; private set; }

    public string RecipientUserId { get; private set; }

    public DateTime ReadAtUtc { get; private set; }

    public static TeamNotificationReadReceipt Create(
        Guid teamNotificationEntryId,
        string recipientUserId,
        DateTime readAtUtc)
    {
        return new TeamNotificationReadReceipt(
            Guid.NewGuid(),
            teamNotificationEntryId,
            recipientUserId,
            readAtUtc);
    }
}
