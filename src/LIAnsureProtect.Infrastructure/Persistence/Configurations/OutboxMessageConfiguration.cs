using LIAnsureProtect.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(message => message.Type)
            .HasColumnName("type")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(message => message.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(message => message.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .IsRequired();

        builder.Property(message => message.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(message => message.ProcessedAtUtc)
            .HasColumnName("processed_at_utc");

        builder.Property(message => message.Error)
            .HasColumnName("error")
            .HasMaxLength(2000);

        builder.Property(message => message.PublishAttemptCount)
            .HasColumnName("publish_attempt_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(message => message.LastPublishAttemptAtUtc)
            .HasColumnName("last_publish_attempt_at_utc");

        builder.Property(message => message.NextAttemptAtUtc)
            .HasColumnName("next_attempt_at_utc");

        builder.Property(message => message.ProviderMessageId)
            .HasColumnName("provider_message_id")
            .HasMaxLength(500);

        builder.Property(message => message.FailedAtUtc)
            .HasColumnName("failed_at_utc");

        builder.HasIndex(message => new
            {
                message.ProcessedAtUtc,
                message.CreatedAtUtc
            })
            .HasDatabaseName("ix_outbox_messages_processed_at_utc_created_at_utc");

        builder.HasIndex(message => new
            {
                message.ProcessedAtUtc,
                message.FailedAtUtc,
                message.NextAttemptAtUtc,
                message.CreatedAtUtc
            })
            .HasDatabaseName("ix_outbox_messages_dispatch_retry");
    }
}
