using LIAnsureProtect.Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class QuoteReferralWorkNoteConfiguration : IEntityTypeConfiguration<QuoteReferralWorkNote>
{
    public void Configure(EntityTypeBuilder<QuoteReferralWorkNote> builder)
    {
        builder.ToTable("quote_referral_work_notes");

        builder.HasKey(note => note.Id);

        builder.Property(note => note.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(note => note.QuoteReferralOperationId)
            .HasColumnName("quote_referral_operation_id")
            .IsRequired();

        builder.Property(note => note.QuoteId)
            .HasColumnName("quote_id")
            .IsRequired();

        builder.Property(note => note.Note)
            .HasColumnName("note")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(note => note.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(note => note.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(note => new
            {
                note.QuoteId,
                note.CreatedAtUtc
            })
            .HasDatabaseName("ix_quote_referral_work_notes_quote_id_created_at_utc");

        builder.HasOne<QuoteReferralOperation>()
            .WithMany(operation => operation.Notes)
            .HasForeignKey(note => note.QuoteReferralOperationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
