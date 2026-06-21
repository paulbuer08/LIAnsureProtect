using LIAnsureProtect.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class PolicyBindingAttemptConfiguration : IEntityTypeConfiguration<PolicyBindingAttempt>
{
    public void Configure(EntityTypeBuilder<PolicyBindingAttempt> builder)
    {
        builder.ToTable("policy_binding_attempts");

        builder.HasKey(attempt => attempt.Id);

        builder.Property(attempt => attempt.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(attempt => attempt.PolicyId)
            .HasColumnName("policy_id")
            .IsRequired();

        builder.Property(attempt => attempt.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(attempt => attempt.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(attempt => attempt.BindingReference)
            .HasColumnName("binding_reference")
            .HasMaxLength(100);

        builder.Property(attempt => attempt.FailureReason)
            .HasColumnName("failure_reason")
            .HasColumnType("text");

        builder.Property(attempt => attempt.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(attempt => attempt.CompletedAtUtc)
            .HasColumnName("completed_at_utc")
            .IsRequired();

        builder.HasIndex(attempt => new
            {
                attempt.PolicyId,
                attempt.CreatedAtUtc
            })
            .HasDatabaseName("ix_policy_binding_attempts_policy_id_created_at_utc");

        builder.HasOne<Policy>()
            .WithMany()
            .HasForeignKey(attempt => attempt.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
