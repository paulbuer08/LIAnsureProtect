using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class QuoteEvidenceRequestReviewConfiguration : IEntityTypeConfiguration<QuoteEvidenceRequestReview>
{
    public void Configure(EntityTypeBuilder<QuoteEvidenceRequestReview> builder)
    {
        builder.ToTable("quote_evidence_request_reviews");

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(review => review.EvidenceRequestId)
            .HasColumnName("evidence_request_id")
            .IsRequired();

        builder.Property(review => review.QuoteId)
            .HasColumnName("quote_id")
            .IsRequired();

        builder.Property(review => review.SubmissionId)
            .HasColumnName("submission_id")
            .IsRequired();

        builder.Property(review => review.OwnerUserId)
            .HasColumnName("owner_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(review => review.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(review => review.Decision)
            .HasColumnName("decision")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(review => review.Reason)
            .HasColumnName("reason")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(review => review.RemediationGuidance)
            .HasColumnName("remediation_guidance")
            .HasMaxLength(2000);

        builder.Property(review => review.ReviewedByUserId)
            .HasColumnName("reviewed_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(review => review.ReviewedAtUtc)
            .HasColumnName("reviewed_at_utc")
            .IsRequired();

        builder.Property(review => review.DocumentCount)
            .HasColumnName("document_count")
            .IsRequired();

        builder.Property(review => review.CleanDocumentCount)
            .HasColumnName("clean_document_count")
            .IsRequired();

        builder.HasIndex(review => new
            {
                review.EvidenceRequestId,
                review.ReviewedAtUtc
            })
            .HasDatabaseName("ix_quote_evidence_request_reviews_request_reviewed_at_utc");

        builder.HasIndex(review => new
            {
                review.QuoteId,
                review.ReviewedAtUtc
            })
            .HasDatabaseName("ix_quote_evidence_request_reviews_quote_reviewed_at_utc");

        builder.HasOne<QuoteEvidenceRequest>()
            .WithMany()
            .HasForeignKey(review => review.EvidenceRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Quote>()
            .WithMany()
            .HasForeignKey(review => review.QuoteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Submission>()
            .WithMany()
            .HasForeignKey(review => review.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
