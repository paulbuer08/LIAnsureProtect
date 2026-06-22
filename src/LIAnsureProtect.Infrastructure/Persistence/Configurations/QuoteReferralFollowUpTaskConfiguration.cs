using LIAnsureProtect.Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class QuoteReferralFollowUpTaskConfiguration : IEntityTypeConfiguration<QuoteReferralFollowUpTask>
{
    public void Configure(EntityTypeBuilder<QuoteReferralFollowUpTask> builder)
    {
        builder.ToTable("quote_referral_follow_up_tasks");

        builder.HasKey(task => task.Id);

        builder.Property(task => task.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(task => task.QuoteReferralOperationId)
            .HasColumnName("quote_referral_operation_id")
            .IsRequired();

        builder.Property(task => task.QuoteId)
            .HasColumnName("quote_id")
            .IsRequired();

        builder.Property(task => task.Title)
            .HasColumnName("title")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(task => task.DueAtUtc)
            .HasColumnName("due_at_utc")
            .IsRequired();

        builder.Property(task => task.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(task => task.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(task => task.CompletedByUserId)
            .HasColumnName("completed_by_user_id")
            .HasMaxLength(256);

        builder.Property(task => task.CompletedAtUtc)
            .HasColumnName("completed_at_utc");

        builder.Ignore(task => task.IsCompleted);

        builder.HasIndex(task => new
            {
                task.QuoteId,
                task.CompletedAtUtc,
                task.DueAtUtc
            })
            .HasDatabaseName("ix_quote_referral_follow_up_tasks_quote_id_completed_due_at_utc");

        builder.HasOne<QuoteReferralOperation>()
            .WithMany(operation => operation.Tasks)
            .HasForeignKey(task => task.QuoteReferralOperationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
