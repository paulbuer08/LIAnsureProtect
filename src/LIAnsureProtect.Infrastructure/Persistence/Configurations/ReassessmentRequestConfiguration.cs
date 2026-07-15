using LIAnsureProtect.Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class ReassessmentRequestConfiguration : IEntityTypeConfiguration<ReassessmentRequest>
{
    public void Configure(EntityTypeBuilder<ReassessmentRequest> builder)
    {
        builder.ToTable("reassessment_requests");
        builder.HasKey(request => request.Id);
        builder.Ignore(request => request.DomainEvents);

        builder.Property(request => request.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(request => request.SubmissionId).HasColumnName("submission_id").IsRequired();
        builder.Property(request => request.BaseQuoteId).HasColumnName("base_quote_id").IsRequired();
        builder.Property(request => request.BaseQuoteVersion).HasColumnName("base_quote_version").IsRequired();
        builder.Property(request => request.OwnerUserId).HasColumnName("owner_user_id").HasMaxLength(256).IsRequired();
        builder.Property(request => request.RequestPayloadJson).HasColumnName("request_payload").HasColumnType("jsonb").IsRequired();
        builder.Property(request => request.RequestFingerprint).HasColumnName("request_fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(request => request.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(request => request.RequestedByUserId).HasColumnName("requested_by_user_id").HasMaxLength(256).IsRequired();
        builder.Property(request => request.RequestedAtUtc).HasColumnName("requested_at_utc").IsRequired();
        builder.Property(request => request.ReviewedByUserId).HasColumnName("reviewed_by_user_id").HasMaxLength(256);
        builder.Property(request => request.ReviewedAtUtc).HasColumnName("reviewed_at_utc");
        builder.Property(request => request.DecisionReason).HasColumnName("decision_reason").HasMaxLength(2000);
        builder.Property(request => request.CreatedQuoteId).HasColumnName("created_quote_id");
        builder.Property(request => request.SubmissionReference).HasColumnName("submission_reference").HasMaxLength(30).IsRequired();
        builder.Property(request => request.CompanyName).HasColumnName("company_name").HasMaxLength(200).IsRequired();
        builder.Property(request => request.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();

        builder.HasIndex(request => new { request.OwnerUserId, request.SubmissionId, request.RequestedAtUtc })
            .HasDatabaseName("ix_reassessment_requests_owner_submission_requested_at_utc");
        builder.HasIndex(request => new { request.Status, request.RequestedAtUtc })
            .HasDatabaseName("ix_reassessment_requests_status_requested_at_utc");
        builder.HasIndex(request => request.SubmissionId)
            .HasFilter("status = 'Pending'")
            .IsUnique()
            .HasDatabaseName("ux_reassessment_requests_submission_pending");

        builder.HasOne<LIAnsureProtect.Domain.Submissions.Submission>()
            .WithMany()
            .HasForeignKey(request => request.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Quote>()
            .WithMany()
            .HasForeignKey(request => request.BaseQuoteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
