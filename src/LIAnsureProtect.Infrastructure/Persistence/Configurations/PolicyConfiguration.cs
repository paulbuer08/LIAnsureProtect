using LIAnsureProtect.Domain.Policies;
using LIAnsureProtect.Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("policies");

        builder.HasKey(policy => policy.Id);

        builder.Ignore(policy => policy.DomainEvents);

        builder.Property(policy => policy.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(policy => policy.QuoteId)
            .HasColumnName("quote_id")
            .IsRequired();

        builder.Property(policy => policy.SubmissionId)
            .HasColumnName("submission_id")
            .IsRequired();

        builder.Property(policy => policy.OwnerUserId)
            .HasColumnName("owner_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(policy => policy.PolicyNumber)
            .HasColumnName("policy_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(policy => policy.Premium)
            .HasColumnName("premium")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(policy => policy.RequestedLimit)
            .HasColumnName("requested_limit")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(policy => policy.Retention)
            .HasColumnName("retention")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(policy => policy.EffectiveDateUtc)
            .HasColumnName("effective_date_utc")
            .IsRequired();

        builder.Property(policy => policy.ExpirationDateUtc)
            .HasColumnName("expiration_date_utc")
            .IsRequired();

        builder.Property(policy => policy.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(policy => policy.BoundByUserId)
            .HasColumnName("bound_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(policy => policy.BoundAtUtc)
            .HasColumnName("bound_at_utc")
            .IsRequired();

        builder.Property(policy => policy.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(policy => policy.QuoteStatusAtBind)
            .HasColumnName("quote_status_at_bind")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(policy => policy.QuoteRiskTierAtBind)
            .HasColumnName("quote_risk_tier_at_bind")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(policy => policy.QuoteSubjectivitiesAtBind)
            .HasColumnName("quote_subjectivities_at_bind")
            .HasColumnType("text")
            .IsRequired();

        builder.HasIndex(policy => policy.QuoteId)
            .IsUnique()
            .HasDatabaseName("ux_policies_quote_id");

        builder.HasIndex(policy => policy.PolicyNumber)
            .IsUnique()
            .HasDatabaseName("ux_policies_policy_number");

        builder.HasIndex(policy => new
            {
                policy.OwnerUserId,
                policy.BoundAtUtc
            })
            .HasDatabaseName("ix_policies_owner_user_id_bound_at_utc");

        builder.HasOne<Quote>()
            .WithMany()
            .HasForeignKey(policy => policy.QuoteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
