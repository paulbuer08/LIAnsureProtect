using LIAnsureProtect.Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class QuoteUnderwritingReviewConfiguration : IEntityTypeConfiguration<QuoteUnderwritingReview>
{
    public void Configure(EntityTypeBuilder<QuoteUnderwritingReview> builder)
    {
        builder.ToTable("quote_underwriting_reviews");

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(review => review.QuoteId)
            .HasColumnName("quote_id")
            .IsRequired();

        builder.Property(review => review.Decision)
            .HasColumnName("decision")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(review => review.ReviewedByUserId)
            .HasColumnName("reviewed_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(review => review.Reason)
            .HasColumnName("reason")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(review => review.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(review => review.PremiumBefore)
            .HasColumnName("premium_before")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(review => review.PremiumAfter)
            .HasColumnName("premium_after")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(review => review.RetentionBefore)
            .HasColumnName("retention_before")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(review => review.RetentionAfter)
            .HasColumnName("retention_after")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(review => review.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(review => new
            {
                review.QuoteId,
                review.CreatedAtUtc
            })
            .HasDatabaseName("ix_quote_underwriting_reviews_quote_id_created_at_utc");

        builder.HasOne<Quote>()
            .WithMany()
            .HasForeignKey(review => review.QuoteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
