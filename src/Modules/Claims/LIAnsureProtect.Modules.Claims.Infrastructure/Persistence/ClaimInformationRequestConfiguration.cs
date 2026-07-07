using LIAnsureProtect.Modules.Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;

public sealed class ClaimInformationRequestConfiguration : IEntityTypeConfiguration<ClaimInformationRequest>
{
    public void Configure(EntityTypeBuilder<ClaimInformationRequest> builder)
    {
        builder.ToTable("claim_information_requests");

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(request => request.ClaimId)
            .HasColumnName("claim_id")
            .IsRequired();

        builder.Property(request => request.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(request => request.Message)
            .HasColumnName("message")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(request => request.RequestedByUserId)
            .HasColumnName("requested_by_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(request => request.RequestedAtUtc)
            .HasColumnName("requested_at_utc")
            .IsRequired();

        builder.Property(request => request.IsAnswered)
            .HasColumnName("is_answered")
            .IsRequired();

        builder.Property(request => request.ResponseText)
            .HasColumnName("response_text")
            .HasMaxLength(4000);

        builder.Property(request => request.RespondedByUserId)
            .HasColumnName("responded_by_user_id")
            .HasMaxLength(256);

        builder.Property(request => request.RespondedAtUtc)
            .HasColumnName("responded_at_utc");

        builder.HasIndex(request => new { request.ClaimId, request.RequestedAtUtc })
            .HasDatabaseName("ix_claim_information_requests_claim_id_requested_at_utc");
    }
}
