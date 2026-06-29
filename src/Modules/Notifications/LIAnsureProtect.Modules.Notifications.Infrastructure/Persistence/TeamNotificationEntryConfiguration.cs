using LIAnsureProtect.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;

public sealed class TeamNotificationEntryConfiguration : IEntityTypeConfiguration<TeamNotificationEntry>
{
    public void Configure(EntityTypeBuilder<TeamNotificationEntry> builder)
    {
        // Schema comes from NotificationsDbContext.HasDefaultSchema("notifications").
        builder.ToTable("team_notification_entries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(entry => entry.Audience)
            .HasColumnName("audience")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entry => entry.Type)
            .HasColumnName("type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entry => entry.SubjectReferenceType)
            .HasColumnName("subject_reference_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entry => entry.SubjectReferenceId)
            .HasColumnName("subject_reference_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entry => entry.AttributesJson)
            .HasColumnName("attributes")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(entry => entry.SourceOutboxMessageId)
            .HasColumnName("source_outbox_message_id")
            .IsRequired();

        builder.Property(entry => entry.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .IsRequired();

        builder.Property(entry => entry.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        // One team entry per source outbox message -> dispatcher retries stay idempotent.
        builder.HasIndex(entry => entry.SourceOutboxMessageId)
            .IsUnique()
            .HasDatabaseName("ix_team_notification_entries_source_outbox_message_id");

        // Fast "this team's inbox, newest first".
        builder.HasIndex(entry => new { entry.Audience, entry.CreatedAtUtc })
            .HasDatabaseName("ix_team_notification_entries_audience_created_at_utc");

        // Per-user read receipts hang off the entry via its backing field.
        builder.HasMany(entry => entry.ReadReceipts)
            .WithOne()
            .HasForeignKey(receipt => receipt.TeamNotificationEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(TeamNotificationEntry.ReadReceipts))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
