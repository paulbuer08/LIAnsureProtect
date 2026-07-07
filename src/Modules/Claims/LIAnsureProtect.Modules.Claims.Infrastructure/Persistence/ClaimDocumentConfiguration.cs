using LIAnsureProtect.Modules.Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;

public sealed class ClaimDocumentConfiguration : IEntityTypeConfiguration<ClaimDocument>
{
    public void Configure(EntityTypeBuilder<ClaimDocument> builder)
    {
        builder.ToTable("claim_documents");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(document => document.ClaimId)
            .HasColumnName("claim_id")
            .IsRequired();

        builder.Property(document => document.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(document => document.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(260)
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
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(document => document.UploadedByUserId)
            .HasColumnName("uploaded_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(document => document.UploadedAtUtc)
            .HasColumnName("uploaded_at_utc")
            .IsRequired();

        builder.Property(document => document.ScanStatus)
            .HasColumnName("scan_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(document => document.ScannerProviderName)
            .HasColumnName("scanner_provider_name")
            .HasMaxLength(200);

        builder.Property(document => document.ScanResultCode)
            .HasColumnName("scan_result_code")
            .HasMaxLength(100);

        builder.Property(document => document.ScanResultReason)
            .HasColumnName("scan_result_reason")
            .HasMaxLength(1000);

        builder.Property(document => document.ScannedAtUtc)
            .HasColumnName("scanned_at_utc");

        builder.Property(document => document.Sha256)
            .HasColumnName("sha256")
            .HasMaxLength(64);

        builder.Ignore(document => document.IsDownloadAvailable);

        builder.HasIndex(document => new { document.ClaimId, document.UploadedAtUtc })
            .HasDatabaseName("ix_claim_documents_claim_id_uploaded_at_utc");
    }
}
