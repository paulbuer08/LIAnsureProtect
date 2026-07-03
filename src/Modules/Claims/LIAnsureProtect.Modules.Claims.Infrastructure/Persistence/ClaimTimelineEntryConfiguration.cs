using LIAnsureProtect.Modules.Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;

public sealed class ClaimTimelineEntryConfiguration : IEntityTypeConfiguration<ClaimTimelineEntry>
{
    public void Configure(EntityTypeBuilder<ClaimTimelineEntry> builder)
    {
        builder.ToTable("claim_timeline_entries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(entry => entry.ClaimId)
            .HasColumnName("claim_id")
            .IsRequired();

        builder.Property(entry => entry.EntryType)
            .HasColumnName("entry_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entry => entry.Summary)
            .HasColumnName("summary")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(entry => entry.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(entry => entry.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(entry => new { entry.ClaimId, entry.CreatedAtUtc })
            .HasDatabaseName("ix_claim_timeline_entries_claim_id_created_at_utc");
    }
}
