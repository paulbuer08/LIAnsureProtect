using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Infrastructure.Persistence.Configurations;

public sealed class QuoteAssuranceDecisionProjectedMessageConfiguration
    : IEntityTypeConfiguration<QuoteAssuranceDecisionProjectedMessage>
{
    public void Configure(EntityTypeBuilder<QuoteAssuranceDecisionProjectedMessage> builder)
    {
        builder.ToTable("quote_assurance_decision_projected_messages");
        builder.HasKey(message => message.SourceOutboxMessageId);
        builder.Property(message => message.SourceOutboxMessageId)
            .HasColumnName("source_outbox_message_id")
            .ValueGeneratedNever();
        builder.Property(message => message.ProjectedAtUtc)
            .HasColumnName("projected_at_utc")
            .IsRequired();
    }
}
