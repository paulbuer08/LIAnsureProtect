using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class QuoteEvidenceResponseConfiguration : IEntityTypeConfiguration<QuoteEvidenceResponse>
{
    public void Configure(EntityTypeBuilder<QuoteEvidenceResponse> builder)
    {
        builder.ToTable("quote_evidence_responses");
        builder.HasKey(response => response.Id);
        builder.Property(response => response.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(response => response.EvidenceRequestId).HasColumnName("evidence_request_id").IsRequired();
        builder.Property(response => response.QuoteId).HasColumnName("quote_id").IsRequired();
        builder.Property(response => response.SubmissionId).HasColumnName("submission_id").IsRequired();
        builder.Property(response => response.OwnerUserId).HasColumnName("owner_user_id").HasMaxLength(256).IsRequired();
        builder.Property(response => response.RespondedByUserId).HasColumnName("responded_by_user_id").HasMaxLength(256).IsRequired();
        builder.Property(response => response.RespondentName).HasColumnName("respondent_name").HasMaxLength(200).IsRequired();
        builder.Property(response => response.RespondentTitle).HasColumnName("respondent_title").HasMaxLength(200).IsRequired();
        builder.Property(response => response.RespondentEmail).HasColumnName("respondent_email").HasMaxLength(320).IsRequired();
        builder.Property(response => response.RespondentPhone).HasColumnName("respondent_phone").HasMaxLength(50);
        builder.Property(response => response.RespondentMobileNumber).HasColumnName("respondent_mobile_number").HasMaxLength(EvidenceResponseFieldRules.MobileNumberMaxLength);
        builder.Property(response => response.RespondentTelephoneNumber).HasColumnName("respondent_telephone_number").HasMaxLength(EvidenceResponseFieldRules.TelephoneNumberMaxLength);
        builder.Property(response => response.ResponseText).HasColumnName("response_text").HasMaxLength(4000);
        builder.Property(response => response.OtherConcerns).HasColumnName("other_concerns").HasMaxLength(4000);
        builder.Property(response => response.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(response => response.RespondedAtUtc).HasColumnName("responded_at_utc").IsRequired();
        builder.Property(response => response.ViewedByUserId).HasColumnName("viewed_by_user_id").HasMaxLength(256);
        builder.Property(response => response.ViewedAtUtc).HasColumnName("viewed_at_utc");
        builder.Property(response => response.EmailDomainStatus).HasColumnName("email_domain_status").HasMaxLength(50).IsRequired();
        builder.Property(response => response.EmailVerificationStatus).HasColumnName("email_verification_status").HasMaxLength(50).IsRequired();
        builder.Property(response => response.EmailVerificationTokenHash).HasColumnName("email_verification_token_hash").HasMaxLength(128);
        builder.Property(response => response.EmailVerificationExpiresAtUtc).HasColumnName("email_verification_expires_at_utc");
        builder.Property(response => response.EmailVerificationSentAtUtc).HasColumnName("email_verification_sent_at_utc");
        builder.Property(response => response.EmailVerifiedAtUtc).HasColumnName("email_verified_at_utc");
        builder.Property(response => response.EmailVerificationSendCount).HasColumnName("email_verification_send_count").IsRequired();

        builder.HasIndex(response => new { response.EvidenceRequestId, response.RespondedAtUtc, response.Id })
            .HasDatabaseName("ix_quote_evidence_responses_request_responded_at");
        builder.HasIndex(response => new { response.OwnerUserId, response.EvidenceRequestId })
            .HasDatabaseName("ix_quote_evidence_responses_owner_request");
        builder.HasIndex(response => new { response.EvidenceRequestId, response.Kind, response.ViewedAtUtc })
            .HasDatabaseName("ix_quote_evidence_responses_request_kind_viewed_at");

        builder.HasOne<QuoteEvidenceRequest>()
            .WithMany()
            .HasForeignKey(response => response.EvidenceRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
