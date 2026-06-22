using LIAnsureProtect.Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class QuoteReferralTimelineEntryConfiguration : IEntityTypeConfiguration<QuoteReferralTimelineEntry>
{
    public void Configure(EntityTypeBuilder<QuoteReferralTimelineEntry> builder)
    {
        builder.ToTable("quote_referral_timeline_entries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(entry => entry.QuoteReferralOperationId)
            .HasColumnName("quote_referral_operation_id")
            .IsRequired();

        builder.Property(entry => entry.QuoteId)
            .HasColumnName("quote_id")
            .IsRequired();

        builder.Property(entry => entry.EntryType)
            .HasColumnName("entry_type")
            .HasConversion<string>()
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(entry => entry.Summary)
            .HasColumnName("summary")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(entry => entry.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(entry => entry.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(entry => new
            {
                entry.QuoteId,
                entry.CreatedAtUtc
            })
            .HasDatabaseName("ix_quote_referral_timeline_entries_quote_id_created_at_utc");

        builder.HasOne<QuoteReferralOperation>()
            .WithMany(operation => operation.TimelineEntries)
            .HasForeignKey(entry => entry.QuoteReferralOperationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
