using LIAnsureProtect.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;

public sealed class TeamNotificationReadReceiptConfiguration : IEntityTypeConfiguration<TeamNotificationReadReceipt>
{
    public void Configure(EntityTypeBuilder<TeamNotificationReadReceipt> builder)
    {
        builder.ToTable("team_notification_read_receipts");

        builder.HasKey(receipt => receipt.Id);

        builder.Property(receipt => receipt.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(receipt => receipt.TeamNotificationEntryId)
            .HasColumnName("team_notification_entry_id")
            .IsRequired();

        builder.Property(receipt => receipt.RecipientUserId)
            .HasColumnName("recipient_user_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(receipt => receipt.ReadAtUtc)
            .HasColumnName("read_at_utc")
            .IsRequired();

        // One receipt per (entry, user): a member's read state is recorded once.
        builder.HasIndex(receipt => new { receipt.TeamNotificationEntryId, receipt.RecipientUserId })
            .IsUnique()
            .HasDatabaseName("ux_team_notification_read_receipts_entry_recipient");
    }
}
