using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Infrastructure.Persistence.Idempotency;
using LIAnsureProtect.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Persistence;

public sealed class SubmissionDbContext(DbContextOptions<SubmissionDbContext> options) : DbContext(options)
{
    public DbSet<Submission> Submissions => Set<Submission>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var submissionsWithDomainEvents = ChangeTracker
            .Entries<Submission>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        var domainEvents = submissionsWithDomainEvents
            .SelectMany(submission => submission.DomainEvents)
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

        foreach (var submission in submissionsWithDomainEvents)
        {
            submission.ClearDomainEvents();
        }

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubmissionDbContext).Assembly);
    }
}
