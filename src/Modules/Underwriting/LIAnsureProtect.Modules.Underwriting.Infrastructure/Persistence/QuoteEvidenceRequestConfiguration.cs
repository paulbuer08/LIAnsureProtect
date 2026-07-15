using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class QuoteEvidenceRequestConfiguration : IEntityTypeConfiguration<QuoteEvidenceRequest>
{
    public void Configure(EntityTypeBuilder<QuoteEvidenceRequest> builder)
    {
        builder.ToTable("quote_evidence_requests");

        builder.HasKey(request => request.Id);

        builder.Ignore(request => request.DomainEvents);

        builder.Property(request => request.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(request => request.QuoteId)
            .HasColumnName("quote_id")
            .IsRequired();

        builder.Property(request => request.QuoteVersion)
            .HasColumnName("quote_version")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(request => request.QuoteDisposition)
            .HasColumnName("quote_disposition")
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(QuoteEvidenceDisposition.Current)
            .IsRequired();

        builder.Property(request => request.SupersededAtUtc)
            .HasColumnName("superseded_at_utc");

        builder.Property(request => request.SupersededByQuoteId)
            .HasColumnName("superseded_by_quote_id");

        builder.Property(request => request.SupersededByQuoteVersion)
            .HasColumnName("superseded_by_quote_version");

        builder.Property(request => request.SubmissionId)
            .HasColumnName("submission_id")
            .IsRequired();

        builder.Property(request => request.SubmissionReference)
            .HasColumnName("submission_reference")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(request => request.CompanyName)
            .HasColumnName("company_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(request => request.OwnerUserId)
            .HasColumnName("owner_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(request => request.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(request => request.DocumentRequirement)
            .HasColumnName("document_requirement")
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(EvidenceDocumentRequirement.Required)
            .IsRequired();

        builder.Property(request => request.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(request => request.Description)
            .HasColumnName("description")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(request => request.DueAtUtc)
            .HasColumnName("due_at_utc")
            .IsRequired();

        builder.Property(request => request.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(request => request.RequestedByUserId)
            .HasColumnName("requested_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(request => request.RequestedAtUtc)
            .HasColumnName("requested_at_utc")
            .IsRequired();

        builder.Property(request => request.RespondedByUserId)
            .HasColumnName("responded_by_user_id")
            .HasMaxLength(256);

        builder.Property(request => request.RespondentName)
            .HasColumnName("respondent_name")
            .HasMaxLength(200);

        builder.Property(request => request.RespondentTitle)
            .HasColumnName("respondent_title")
            .HasMaxLength(200);

        builder.Property(request => request.RespondentEmail)
            .HasColumnName("respondent_email")
            .HasMaxLength(320);

        builder.Property(request => request.RespondentPhone)
            .HasColumnName("respondent_phone")
            .HasMaxLength(50);

        builder.Property(request => request.RespondentMobileNumber)
            .HasColumnName("respondent_mobile_number")
            .HasMaxLength(EvidenceResponseFieldRules.MobileNumberMaxLength);

        builder.Property(request => request.RespondentTelephoneNumber)
            .HasColumnName("respondent_telephone_number")
            .HasMaxLength(EvidenceResponseFieldRules.TelephoneNumberMaxLength);

        builder.Property(request => request.ResponseText)
            .HasColumnName("response_text")
            .HasMaxLength(4000);

        builder.Property(request => request.OtherConcerns)
            .HasColumnName("other_concerns")
            .HasMaxLength(4000);

        builder.Property(request => request.AttachmentFileName)
            .HasColumnName("attachment_file_name")
            .HasMaxLength(512);

        builder.Property(request => request.AttachmentContentType)
            .HasColumnName("attachment_content_type")
            .HasMaxLength(200);

        builder.Property(request => request.AttachmentSizeBytes)
            .HasColumnName("attachment_size_bytes");

        builder.Property(request => request.RespondedAtUtc)
            .HasColumnName("responded_at_utc");

        builder.Property(request => request.AcceptedByUserId)
            .HasColumnName("accepted_by_user_id")
            .HasMaxLength(256);

        builder.Property(request => request.AcceptedAtUtc)
            .HasColumnName("accepted_at_utc");

        builder.Property(request => request.CancelledByUserId)
            .HasColumnName("cancelled_by_user_id")
            .HasMaxLength(256);

        builder.Property(request => request.CancelledAtUtc)
            .HasColumnName("cancelled_at_utc");

        builder.Property(request => request.ReviewNotes)
            .HasColumnName("review_notes")
            .HasMaxLength(2000);

        builder.Property(request => request.ReviewDecision)
            .HasColumnName("review_decision")
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(EvidenceReviewDecisionStatus.NotReviewed)
            .IsRequired();

        builder.Property(request => request.ReviewReason)
            .HasColumnName("review_reason")
            .HasMaxLength(2000);

        builder.Property(request => request.RemediationGuidance)
            .HasColumnName("remediation_guidance")
            .HasMaxLength(2000);

        builder.Property(request => request.ReviewedByUserId)
            .HasColumnName("reviewed_by_user_id")
            .HasMaxLength(256);

        builder.Property(request => request.ReviewedAtUtc)
            .HasColumnName("reviewed_at_utc");

        builder.Property(request => request.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(request => request.Version)
            .HasColumnName("version")
            .HasDefaultValue(0)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(request => new
            {
                request.OwnerUserId,
                request.Status,
                request.DueAtUtc
            })
            .HasDatabaseName("ix_quote_evidence_requests_owner_status_due_at_utc");

        builder.HasIndex(request => new
            {
                request.QuoteId,
                request.Status,
                request.UpdatedAtUtc
            })
            .HasDatabaseName("ix_quote_evidence_requests_quote_status_updated_at_utc");

        builder.HasIndex(request => new
            {
                request.SubmissionId,
                request.QuoteDisposition,
                request.QuoteVersion
            })
            .HasDatabaseName("ix_quote_evidence_requests_submission_disposition_version");
    }
}
