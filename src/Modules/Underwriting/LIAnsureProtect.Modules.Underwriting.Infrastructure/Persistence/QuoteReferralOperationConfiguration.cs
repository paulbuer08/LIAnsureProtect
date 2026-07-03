using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class QuoteReferralOperationConfiguration : IEntityTypeConfiguration<QuoteReferralOperation>
{
    public void Configure(EntityTypeBuilder<QuoteReferralOperation> builder)
    {
        builder.ToTable("quote_referral_operations");

        builder.HasKey(operation => operation.Id);

        builder.Property(operation => operation.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(operation => operation.QuoteId)
            .HasColumnName("quote_id")
            .IsRequired();

        builder.Property(operation => operation.AssignedUnderwriterUserId)
            .HasColumnName("assigned_underwriter_user_id")
            .HasMaxLength(256);

        builder.Property(operation => operation.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(operation => operation.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(operation => operation.DueAtUtc)
            .HasColumnName("due_at_utc")
            .IsRequired();

        builder.Property(operation => operation.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(operation => operation.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(operation => operation.ClosedAtUtc)
            .HasColumnName("closed_at_utc");

        // Optimistic concurrency: the domain bumps Version on every mutation and EF includes the
        // original value in the UPDATE's WHERE clause, so racing writers (e.g. two underwriters
        // both clicking "Assign to me") fail loudly instead of silently overwriting each other.
        builder.Property(operation => operation.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(operation => operation.QuoteId)
            .IsUnique()
            .HasDatabaseName("ux_quote_referral_operations_quote_id");

        builder.HasIndex(operation => new
            {
                operation.Status,
                operation.Priority,
                operation.DueAtUtc
            })
            .HasDatabaseName("ix_quote_referral_operations_status_priority_due_at_utc");

        builder.Navigation(operation => operation.Notes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(operation => operation.Tasks)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(operation => operation.TimelineEntries)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
