using LIAnsureProtect.Modules.Claims.Domain;
using LIAnsureProtect.Platform.Abstractions.DomainEvents;
using LIAnsureProtect.Platform.Outbox;
using LIAnsureProtect.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;

/// <summary>
/// The Claims module's own DbContext, owning the dedicated <c>claims</c> schema. Domain events
/// raised by claim aggregates are captured transactionally into the module's own outbox table
/// (<c>claims.outbox_messages</c>) — the same template as the Underwriting module.
/// </summary>
public sealed class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options)
    : ModuleDbContext(options)
{
    public const string SchemaName = "claims";

    public DbSet<Claim> Claims => Set<Claim>();

    public DbSet<ClaimTimelineEntry> ClaimTimelineEntries => Set<ClaimTimelineEntry>();

    public DbSet<ClaimDocument> ClaimDocuments => Set<ClaimDocument>();

    public DbSet<ModuleOutboxMessage> OutboxMessages => Set<ModuleOutboxMessage>();

    protected override string? Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClaimsDbContext).Assembly);
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
