using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class ReferralOperationProjectedMessageConfiguration
    : IEntityTypeConfiguration<ReferralOperationProjectedMessage>
{
    public void Configure(EntityTypeBuilder<ReferralOperationProjectedMessage> builder)
    {
        builder.ToTable("referral_operation_projected_messages");
        builder.HasKey(message => message.SourceOutboxMessageId);
        builder.Property(message => message.SourceOutboxMessageId)
            .HasColumnName("source_outbox_message_id")
            .ValueGeneratedNever();
        builder.Property(message => message.AppliedAtUtc)
            .HasColumnName("applied_at_utc")
            .IsRequired();
    }
}
