using LIAnsureProtect.Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class QuoteRatingProviderAttemptConfiguration
    : IEntityTypeConfiguration<QuoteRatingProviderAttempt>
{
    public void Configure(EntityTypeBuilder<QuoteRatingProviderAttempt> builder)
    {
        builder.ToTable("quote_rating_provider_attempts");

        builder.HasKey(attempt => attempt.Id);

        builder.Property(attempt => attempt.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(attempt => attempt.QuoteId)
            .HasColumnName("quote_id")
            .IsRequired();

        builder.Property(attempt => attempt.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(attempt => attempt.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(attempt => attempt.MarketDisposition)
            .HasColumnName("market_disposition")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(attempt => attempt.ProviderReference)
            .HasColumnName("provider_reference")
            .HasMaxLength(200);

        builder.Property(attempt => attempt.ProviderQuoteNumber)
            .HasColumnName("provider_quote_number")
            .HasMaxLength(200);

        builder.Property(attempt => attempt.IndicatedPremium)
            .HasColumnName("indicated_premium")
            .HasPrecision(12, 2);

        builder.Property(attempt => attempt.IndicatedLimit)
            .HasColumnName("indicated_limit")
            .HasPrecision(12, 2);

        builder.Property(attempt => attempt.IndicatedRetention)
            .HasColumnName("indicated_retention")
            .HasPrecision(12, 2);

        builder.Property(attempt => attempt.HttpStatusCode)
            .HasColumnName("http_status_code");

        builder.Property(attempt => attempt.FailureCategory)
            .HasColumnName("failure_category")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(attempt => attempt.FailureReason)
            .HasColumnName("failure_reason")
            .HasColumnType("text");

        builder.Property(attempt => attempt.AttemptCount)
            .HasColumnName("attempt_count")
            .IsRequired();

        builder.Property(attempt => attempt.DurationMs)
            .HasColumnName("duration_ms")
            .IsRequired();

        builder.Property(attempt => attempt.RequestPayloadHash)
            .HasColumnName("request_payload_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(attempt => attempt.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(attempt => attempt.CompletedAtUtc)
            .HasColumnName("completed_at_utc")
            .IsRequired();

        builder.HasIndex(attempt => new
            {
                attempt.QuoteId,
                attempt.CreatedAtUtc
            })
            .HasDatabaseName("ix_quote_rating_provider_attempts_quote_id_created_at_utc");

        builder.HasIndex(attempt => new
            {
                attempt.Status,
                attempt.CreatedAtUtc
            })
            .HasDatabaseName("ix_quote_rating_provider_attempts_status_created_at_utc");

        builder.HasOne(attempt => attempt.Quote)
            .WithMany()
            .HasForeignKey(attempt => attempt.QuoteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
