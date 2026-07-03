using LIAnsureProtect.Modules.Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;

public sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("claims");

        builder.HasKey(claim => claim.Id);

        builder.Property(claim => claim.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(claim => claim.PolicyId)
            .HasColumnName("policy_id")
            .IsRequired();

        builder.Property(claim => claim.SubmissionId)
            .HasColumnName("submission_id")
            .IsRequired();

        builder.Property(claim => claim.OwnerUserId)
            .HasColumnName("owner_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(claim => claim.ClaimNumber)
            .HasColumnName("claim_number")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(claim => claim.IncidentType)
            .HasColumnName("incident_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(claim => claim.IncidentAtUtc)
            .HasColumnName("incident_at_utc")
            .IsRequired();

        builder.Property(claim => claim.DiscoveredAtUtc)
            .HasColumnName("discovered_at_utc")
            .IsRequired();

        builder.Property(claim => claim.Description)
            .HasColumnName("description")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(claim => claim.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(claim => claim.AssignedAdjusterUserId)
            .HasColumnName("assigned_adjuster_user_id")
            .HasMaxLength(256);

        builder.Property(claim => claim.PolicyNumberAtFiling)
            .HasColumnName("policy_number_at_filing")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(claim => claim.PolicyEffectiveAtFiling)
            .HasColumnName("policy_effective_at_filing")
            .IsRequired();

        builder.Property(claim => claim.PolicyExpirationAtFiling)
            .HasColumnName("policy_expiration_at_filing")
            .IsRequired();

        builder.Property(claim => claim.ClaimedAmount)
            .HasColumnName("claimed_amount")
            .HasPrecision(18, 2);

        builder.Property(claim => claim.ReserveAmount)
            .HasColumnName("reserve_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(claim => claim.PaidAmount)
            .HasColumnName("paid_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(claim => claim.PolicyLimitAtFiling)
            .HasColumnName("policy_limit_at_filing")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(claim => claim.PolicyRetentionAtFiling)
            .HasColumnName("policy_retention_at_filing")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(claim => claim.FiledAtUtc)
            .HasColumnName("filed_at_utc")
            .IsRequired();

        builder.Property(claim => claim.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        // Optimistic concurrency: the domain bumps Version on every mutation and EF includes the
        // original value in the UPDATE's WHERE clause, so racing writers (two adjusters claiming
        // the same file in CM2) fail loudly instead of silently overwriting each other.
        builder.Property(claim => claim.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(claim => claim.ClaimNumber)
            .IsUnique()
            .HasDatabaseName("ux_claims_claim_number");

        builder.HasIndex(claim => claim.OwnerUserId)
            .HasDatabaseName("ix_claims_owner_user_id");

        builder.HasIndex(claim => claim.PolicyId)
            .HasDatabaseName("ix_claims_policy_id");

        builder.HasIndex(claim => new { claim.Status, claim.FiledAtUtc })
            .HasDatabaseName("ix_claims_status_filed_at_utc");

        builder.HasIndex(claim => claim.AssignedAdjusterUserId)
            .HasDatabaseName("ix_claims_assigned_adjuster_user_id");

        builder.HasMany(claim => claim.TimelineEntries)
            .WithOne()
            .HasForeignKey(entry => entry.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(claim => claim.WorkNotes)
            .WithOne()
            .HasForeignKey(note => note.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(claim => claim.InformationRequests)
            .WithOne()
            .HasForeignKey(request => request.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(claim => claim.Documents)
            .WithOne()
            .HasForeignKey(document => document.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(claim => claim.ReserveChanges)
            .WithOne()
            .HasForeignKey(change => change.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(claim => claim.TimelineEntries)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(claim => claim.WorkNotes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(claim => claim.InformationRequests)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(claim => claim.Documents)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(claim => claim.ReserveChanges)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(claim => claim.DomainEvents);
    }
}
