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
        builder.Property(response => response.ResponseText).HasColumnName("response_text").HasMaxLength(4000);
        builder.Property(response => response.OtherConcerns).HasColumnName("other_concerns").HasMaxLength(4000);
        builder.Property(response => response.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(response => response.RespondedAtUtc).HasColumnName("responded_at_utc").IsRequired();

        builder.HasIndex(response => new { response.EvidenceRequestId, response.RespondedAtUtc, response.Id })
            .HasDatabaseName("ix_quote_evidence_responses_request_responded_at");
        builder.HasIndex(response => new { response.OwnerUserId, response.EvidenceRequestId })
            .HasDatabaseName("ix_quote_evidence_responses_owner_request");

        builder.HasOne<QuoteEvidenceRequest>()
            .WithMany()
            .HasForeignKey(response => response.EvidenceRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
