using LIAnsureProtect.Modules.Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;

public sealed class ClaimWorkNoteConfiguration : IEntityTypeConfiguration<ClaimWorkNote>
{
    public void Configure(EntityTypeBuilder<ClaimWorkNote> builder)
    {
        builder.ToTable("claim_work_notes");

        builder.HasKey(note => note.Id);

        builder.Property(note => note.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(note => note.ClaimId)
            .HasColumnName("claim_id")
            .IsRequired();

        builder.Property(note => note.Note)
            .HasColumnName("note")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(note => note.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(note => note.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(note => new { note.ClaimId, note.CreatedAtUtc })
            .HasDatabaseName("ix_claim_work_notes_claim_id_created_at_utc");
    }
}
