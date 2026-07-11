using LIAnsureProtect.Modules.Underwriting.Domain;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence.Documents;
using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using LIAnsureProtect.Platform.Abstractions.DomainEvents;
using LIAnsureProtect.Platform.Outbox;
using LIAnsureProtect.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

/// <summary>
/// The Underwriting module's own DbContext, owning the dedicated <c>underwriting</c> schema.
/// In this first slice it holds only the advisory AI review audit; later slices add referral
/// operations, evidence, and the decision audit.
/// </summary>
public sealed class UnderwritingDbContext(DbContextOptions<UnderwritingDbContext> options)
    : ModuleDbContext(options)
{
    public const string SchemaName = "underwriting";

    public DbSet<AiUnderwritingReview> AiUnderwritingReviews => Set<AiUnderwritingReview>();

    public DbSet<QuoteReferralOperation> QuoteReferralOperations => Set<QuoteReferralOperation>();

    public DbSet<ReferralOperationProjectedMessage> ReferralOperationProjectedMessages
        => Set<ReferralOperationProjectedMessage>();

    public DbSet<QuoteEvidenceRequest> QuoteEvidenceRequests => Set<QuoteEvidenceRequest>();

    public DbSet<QuoteEvidenceRequestReview> QuoteEvidenceRequestReviews => Set<QuoteEvidenceRequestReview>();

    public DbSet<QuoteEvidenceDocument> QuoteEvidenceDocuments => Set<QuoteEvidenceDocument>();

    public DbSet<QuoteAssuranceProjectedMessage> QuoteAssuranceProjectedMessages
        => Set<QuoteAssuranceProjectedMessage>();

    public DbSet<ModuleOutboxMessage> OutboxMessages => Set<ModuleOutboxMessage>();

    protected override string? Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UnderwritingDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new ModuleOutboxMessageConfiguration());
    }

    protected override async Task CaptureDomainEventsAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken)
    {
        var createdAtUtc = DateTime.UtcNow;
        var outboxMessages = domainEvents
            .Select(domainEvent => ModuleOutboxMessage.FromDomainEvent(domainEvent, createdAtUtc))
            .ToList();

        await OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
    }
}
