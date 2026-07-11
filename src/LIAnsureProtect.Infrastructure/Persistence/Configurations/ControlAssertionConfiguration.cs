using LIAnsureProtect.Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class ControlAssertionConfiguration : IEntityTypeConfiguration<ControlAssertion>
{
    public void Configure(EntityTypeBuilder<ControlAssertion> builder)
    {
        builder.ToTable("quote_control_assertions");
        builder.HasKey(assertion => assertion.Id);

        builder.Property(assertion => assertion.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(assertion => assertion.QuoteId).HasColumnName("quote_id").IsRequired();
        builder.Property(assertion => assertion.QuoteVersion).HasColumnName("quote_version").IsRequired();
        builder.Property(assertion => assertion.ControlType).HasColumnName("control_type").HasConversion<string>().HasMaxLength(80).IsRequired();
        builder.Property(assertion => assertion.ClaimedState).HasColumnName("claimed_state").HasMaxLength(100).IsRequired();
        builder.Property(assertion => assertion.AssuranceState).HasColumnName("assurance_state").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(assertion => assertion.EvidenceRequired).HasColumnName("evidence_required").IsRequired();
        builder.Property(assertion => assertion.EvidenceReason).HasColumnName("evidence_reason").HasColumnType("text").IsRequired();
        builder.Property(assertion => assertion.CapturedAtUtc).HasColumnName("captured_at_utc").IsRequired();
        builder.Property(assertion => assertion.VerifiedByUserId).HasColumnName("verified_by_user_id").HasMaxLength(256);
        builder.Property(assertion => assertion.VerifiedAtUtc).HasColumnName("verified_at_utc");

        builder.HasIndex(assertion => new { assertion.QuoteId, assertion.ControlType })
            .IsUnique()
            .HasDatabaseName("ux_quote_control_assertions_quote_id_control_type");
    }
}
