using LIAnsureProtect.Domain.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("submissions");

        builder.HasKey(submission => submission.Id);

        builder.Ignore(submission => submission.DomainEvents);

        builder.Property(submission => submission.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(submission => submission.Reference)
            .HasColumnName("reference")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(submission => submission.OwnerUserId)
            .HasColumnName("owner_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(submission => submission.ApplicantName)
            .HasColumnName("applicant_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(submission => submission.ApplicantEmail)
            .HasColumnName("applicant_email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(submission => submission.CompanyName)
            .HasColumnName("company_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(submission => submission.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(submission => submission.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(submission => new
            {
                submission.OwnerUserId,
                submission.CreatedAtUtc
            })
            .HasDatabaseName("ix_submissions_owner_user_id_created_at_utc");

        builder.HasIndex(submission => submission.Reference)
            .IsUnique()
            .HasDatabaseName("ux_submissions_reference");
    }
}
