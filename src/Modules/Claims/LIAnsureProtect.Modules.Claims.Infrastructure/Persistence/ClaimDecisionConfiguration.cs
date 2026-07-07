using LIAnsureProtect.Modules.Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;

public sealed class ClaimDecisionConfiguration : IEntityTypeConfiguration<ClaimDecision>
{
    public void Configure(EntityTypeBuilder<ClaimDecision> builder)
    {
        builder.ToTable("claim_decisions");

        builder.HasKey(decision => decision.Id);

        builder.Property(decision => decision.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(decision => decision.ClaimId)
            .HasColumnName("claim_id")
            .IsRequired();

        builder.Property(decision => decision.Outcome)
            .HasColumnName("outcome")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(decision => decision.SettlementAmount)
            .HasColumnName("settlement_amount")
            .HasPrecision(18, 2);

        builder.Property(decision => decision.DenialReason)
            .HasColumnName("denial_reason")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(decision => decision.Reason)
            .HasColumnName("reason")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(decision => decision.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.Property(decision => decision.ClaimedAmountAtDecision)
            .HasColumnName("claimed_amount_at_decision")
            .HasPrecision(18, 2);

        builder.Property(decision => decision.ReserveAmountAtDecision)
            .HasColumnName("reserve_amount_at_decision")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(decision => decision.DecidedByUserId)
            .HasColumnName("decided_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(decision => decision.DecidedAtUtc)
            .HasColumnName("decided_at_utc")
            .IsRequired();

        builder.HasIndex(decision => new { decision.ClaimId, decision.DecidedAtUtc })
            .HasDatabaseName("ix_claim_decisions_claim_id_decided_at_utc");
    }
}
