using LIAnsureProtect.Platform.Abstractions.DomainEvents;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Platform.Persistence;

/// <summary>
/// Base class for every bounded-context module's <see cref="DbContext"/>.
/// <para>
/// It bakes in the two rules of the modular monolith's persistence:
/// </para>
/// <list type="number">
///   <item><b>Schema-per-module</b> — each module owns its own PostgreSQL schema via
///   <see cref="Schema"/>, so modules never share tables.</item>
///   <item><b>Transactional domain-event capture</b> — on save, it collects domain events
///   from tracked aggregates, hands them to <see cref="CaptureDomainEventsAsync"/> for
///   persistence (e.g. as outbox rows) inside the <em>same</em> transaction, then clears them.</item>
/// </list>
/// <para>
/// In Milestone 32 this is the reusable template; the legacy <c>SubmissionDbContext</c> keeps its
/// own equivalent logic untouched (no table moves). The first real module context to inherit this
/// arrives in Milestone 33.
/// </para>
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    protected ModuleDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// The PostgreSQL schema this module owns (e.g. <c>notifications</c>). Return <c>null</c> to use
    /// the default (<c>public</c>) schema — useful while a context has not been moved to its own schema yet.
    /// </summary>
    protected abstract string? Schema { get; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithDomainEvents = ChangeTracker
            .Entries()
            .Where(entry => entry.Entity is IHasDomainEvents source && source.DomainEvents.Count > 0)
            .Select(entry => (IHasDomainEvents)entry.Entity)
            .ToList();

        var domainEvents = entitiesWithDomainEvents
            .SelectMany(entity => entity.DomainEvents)
            .ToList();

        if (domainEvents.Count > 0)
        {
            // Persist the events BEFORE base.SaveChangesAsync so they commit in the same transaction
            // as the business change — the transactional outbox guarantee.
            await CaptureDomainEventsAsync(domainEvents, cancellationToken);
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var entity in entitiesWithDomainEvents)
        {
            entity.ClearDomainEvents();
        }

        return result;
    }

    /// <summary>
    /// Override to persist the collected domain events (typically by adding outbox rows to this
    /// context) so they commit atomically with the business change. The default does nothing.
    /// </summary>
    protected virtual Task CaptureDomainEventsAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken) => Task.CompletedTask;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (!string.IsNullOrWhiteSpace(Schema))
        {
            modelBuilder.HasDefaultSchema(Schema);
        }

        base.OnModelCreating(modelBuilder);
    }
}
