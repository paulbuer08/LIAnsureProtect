using LIAnsureProtect.Modules.Underwriting.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class AiUnderwritingReviewConfiguration : IEntityTypeConfiguration<AiUnderwritingReview>
{
    public void Configure(EntityTypeBuilder<AiUnderwritingReview> builder)
    {
        // Schema comes from UnderwritingDbContext.HasDefaultSchema("underwriting").
        builder.ToTable("ai_underwriting_reviews");

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Quote is referenced by id only (no cross-context navigation or foreign key).
        builder.Property(review => review.QuoteId)
            .HasColumnName("quote_id")
            .IsRequired();

        builder.Property(review => review.RequestedByUserId)
            .HasColumnName("requested_by_user_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(review => review.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(review => review.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(review => review.PromptVersion)
            .HasColumnName("prompt_version")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(review => review.OutputSchemaVersion)
            .HasColumnName("output_schema_version")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(review => review.InputSnapshotHash)
            .HasColumnName("input_snapshot_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(review => review.ExecutiveSummary)
            .HasColumnName("executive_summary")
            .HasColumnType("text");

        builder.Property(review => review.PositiveRiskSignals)
            .HasColumnName("positive_risk_signals")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(review => review.NegativeRiskSignals)
            .HasColumnName("negative_risk_signals")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(review => review.ControlGaps)
            .HasColumnName("control_gaps")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(review => review.SuggestedUnderwritingQuestions)
            .HasColumnName("suggested_underwriting_questions")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(review => review.SuggestedSubjectivityCandidates)
            .HasColumnName("suggested_subjectivity_candidates")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(review => review.Citations)
            .HasColumnName("citations")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(review => review.Limitations)
            .HasColumnName("limitations")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(review => review.AdvisoryDisclaimer)
            .HasColumnName("advisory_disclaimer")
            .HasColumnType("text");

        builder.Property(review => review.FailureReason)
            .HasColumnName("failure_reason")
            .HasColumnType("text");

        builder.Property(review => review.Feedback)
            .HasColumnName("feedback")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(review => review.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(review => review.CompletedAtUtc)
            .HasColumnName("completed_at_utc")
            .IsRequired();

        builder.HasIndex(review => new { review.QuoteId, review.CreatedAtUtc })
            .HasDatabaseName("ix_ai_underwriting_reviews_quote_id_created_at_utc");

        builder.HasIndex(review => new { review.Status, review.CreatedAtUtc })
            .HasDatabaseName("ix_ai_underwriting_reviews_status_created_at_utc");
    }
}
