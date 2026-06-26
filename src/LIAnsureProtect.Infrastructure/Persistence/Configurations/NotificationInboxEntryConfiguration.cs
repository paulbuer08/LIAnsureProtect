using LIAnsureProtect.Infrastructure.Persistence.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class NotificationInboxEntryConfiguration : IEntityTypeConfiguration<NotificationInboxEntry>
{
    public void Configure(EntityTypeBuilder<NotificationInboxEntry> builder)
    {
        builder.ToTable("notification_inbox_entries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(entry => entry.RecipientUserId)
            .HasColumnName("recipient_user_id")
            .HasMaxLength(200)
            .IsRequired();

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

        builder.Property(entry => entry.ReadAtUtc)
            .HasColumnName("read_at_utc");

        // One inbox entry per source outbox message -> dispatcher retries stay idempotent.
        builder.HasIndex(entry => entry.SourceOutboxMessageId)
            .IsUnique()
            .HasDatabaseName("ix_notification_inbox_entries_source_outbox_message_id");

        // Fast "my inbox" and "my unread count" reads.
        builder.HasIndex(entry => new { entry.RecipientUserId, entry.ReadAtUtc })
            .HasDatabaseName("ix_notification_inbox_entries_recipient_read");
    }
}
