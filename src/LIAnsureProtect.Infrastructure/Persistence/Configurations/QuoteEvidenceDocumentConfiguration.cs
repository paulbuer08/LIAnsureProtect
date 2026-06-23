using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class QuoteEvidenceDocumentConfiguration : IEntityTypeConfiguration<QuoteEvidenceDocument>
{
    public void Configure(EntityTypeBuilder<QuoteEvidenceDocument> builder)
    {
        builder.ToTable("quote_evidence_documents");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(document => document.EvidenceRequestId)
            .HasColumnName("evidence_request_id")
            .IsRequired();

        builder.Property(document => document.QuoteId)
            .HasColumnName("quote_id")
            .IsRequired();

        builder.Property(document => document.SubmissionId)
            .HasColumnName("submission_id")
            .IsRequired();

        builder.Property(document => document.OwnerUserId)
            .HasColumnName("owner_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(document => document.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(document => document.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(document => document.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(document => document.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(document => document.UploadedByUserId)
            .HasColumnName("uploaded_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(document => document.UploadedAtUtc)
            .HasColumnName("uploaded_at_utc")
            .IsRequired();

        builder.HasIndex(document => new
            {
                document.EvidenceRequestId,
                document.UploadedAtUtc
            })
            .HasDatabaseName("ix_quote_evidence_documents_request_uploaded_at_utc");

        builder.HasIndex(document => new
            {
                document.OwnerUserId,
                document.EvidenceRequestId
            })
            .HasDatabaseName("ix_quote_evidence_documents_owner_request");

        builder.HasIndex(document => document.StorageKey)
            .IsUnique()
            .HasDatabaseName("ux_quote_evidence_documents_storage_key");

        builder.HasOne<QuoteEvidenceRequest>()
            .WithMany()
            .HasForeignKey(document => document.EvidenceRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Quote>()
            .WithMany()
            .HasForeignKey(document => document.QuoteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Submission>()
            .WithMany()
            .HasForeignKey(document => document.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
