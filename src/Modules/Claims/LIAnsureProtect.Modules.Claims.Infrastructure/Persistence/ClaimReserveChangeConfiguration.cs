using LIAnsureProtect.Modules.Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;

public sealed class ClaimReserveChangeConfiguration : IEntityTypeConfiguration<ClaimReserveChange>
{
    public void Configure(EntityTypeBuilder<ClaimReserveChange> builder)
    {
        builder.ToTable("claim_reserve_changes");

        builder.HasKey(change => change.Id);

        builder.Property(change => change.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(change => change.ClaimId)
            .HasColumnName("claim_id")
            .IsRequired();

        builder.Property(change => change.OldAmount)
            .HasColumnName("old_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(change => change.NewAmount)
            .HasColumnName("new_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(change => change.Reason)
            .HasColumnName("reason")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(change => change.ChangedByUserId)
            .HasColumnName("changed_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(change => change.ChangedAtUtc)
            .HasColumnName("changed_at_utc")
            .IsRequired();

        builder.HasIndex(change => new { change.ClaimId, change.ChangedAtUtc })
            .HasDatabaseName("ix_claim_reserve_changes_claim_id_changed_at_utc");
    }
}
