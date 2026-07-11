using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class QuoteAssuranceProjectedMessageConfiguration
    : IEntityTypeConfiguration<QuoteAssuranceProjectedMessage>
{
    public void Configure(EntityTypeBuilder<QuoteAssuranceProjectedMessage> builder)
    {
        builder.ToTable("quote_assurance_projected_messages", UnderwritingDbContext.SchemaName);
        builder.HasKey(message => message.SourceOutboxMessageId);
        builder.Property(message => message.SourceOutboxMessageId)
            .HasColumnName("source_outbox_message_id")
            .ValueGeneratedNever();
        builder.Property(message => message.ProjectedAtUtc)
            .HasColumnName("projected_at_utc")
            .IsRequired();
    }
}
