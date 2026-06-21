using LIAnsureProtect.Domain.Common;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Infrastructure.Persistence.Idempotency;
using LIAnsureProtect.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Persistence;

public sealed class SubmissionDbContext(DbContextOptions<SubmissionDbContext> options) : DbContext(options)
{
    public DbSet<Submission> Submissions => Set<Submission>();

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<QuoteRatingProviderAttempt> QuoteRatingProviderAttempts => Set<QuoteRatingProviderAttempt>();

    public DbSet<QuoteUnderwritingReview> QuoteUnderwritingReviews => Set<QuoteUnderwritingReview>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithDomainEvents = ChangeTracker
            .Entries()
            .Where(entry => entry.Entity is IHasDomainEvents domainEventSource
                && domainEventSource.DomainEvents.Count > 0)
            .Select(entry => (IHasDomainEvents)entry.Entity)
            .ToList();

        var domainEvents = entitiesWithDomainEvents
            .SelectMany(entity => entity.DomainEvents)
            .ToList();

        if (domainEvents.Count > 0)
        {
            var createdAtUtc = DateTime.UtcNow;
            var outboxMessages = domainEvents
                .Select(domainEvent => OutboxMessage.FromDomainEvent(domainEvent, createdAtUtc))
                .ToList();

            await OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var entity in entitiesWithDomainEvents)
        {
            entity.ClearDomainEvents();
        }

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubmissionDbContext).Assembly);
    }
}
