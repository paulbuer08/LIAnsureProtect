using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quotes");

        builder.HasKey(quote => quote.Id);

        builder.Ignore(quote => quote.DomainEvents);

        builder.Property(quote => quote.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(quote => quote.SubmissionId)
            .HasColumnName("submission_id")
            .IsRequired();

        builder.Property(quote => quote.OwnerUserId)
            .HasColumnName("owner_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(quote => quote.Premium)
            .HasColumnName("premium")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(quote => quote.RequestedLimit)
            .HasColumnName("requested_limit")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(quote => quote.Retention)
            .HasColumnName("retention")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(quote => quote.RiskTier)
            .HasColumnName("risk_tier")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(quote => quote.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(quote => quote.StrategyName)
            .HasColumnName("strategy_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(quote => quote.Subjectivities)
            .HasColumnName("subjectivities")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(quote => quote.ReferralReasons)
            .HasColumnName("referral_reasons")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(quote => quote.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(quote => quote.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .IsRequired();

        builder.HasIndex(quote => new
            {
                quote.OwnerUserId,
                quote.CreatedAtUtc
            })
            .HasDatabaseName("ix_quotes_owner_user_id_created_at_utc");

        builder.HasIndex(quote => quote.SubmissionId)
            .HasDatabaseName("ix_quotes_submission_id");

        builder.HasOne<Submission>()
            .WithMany()
            .HasForeignKey(quote => quote.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
