using LIAnsureProtect.Infrastructure.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(record => record.Key)
            .HasColumnName("key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(record => record.OwnerUserId)
            .HasColumnName("owner_user_id")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(record => record.ActionName)
            .HasColumnName("action_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(record => record.RequestFingerprint)
            .HasColumnName("request_fingerprint")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(record => record.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(record => record.ResponseStatusCode)
            .HasColumnName("response_status_code");

        builder.Property(record => record.ResponseBody)
            .HasColumnName("response_body")
            .HasColumnType("jsonb");

        builder.Property(record => record.ResponseContentType)
            .HasColumnName("response_content_type")
            .HasMaxLength(200);

        builder.Property(record => record.ResponseLocation)
            .HasColumnName("response_location")
            .HasMaxLength(1000);

        builder.Property(record => record.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(record => record.CompletedAtUtc)
            .HasColumnName("completed_at_utc");

        builder.HasIndex(record => record.Key)
            .IsUnique()
            .HasDatabaseName("ux_idempotency_records_key");
    }
}
