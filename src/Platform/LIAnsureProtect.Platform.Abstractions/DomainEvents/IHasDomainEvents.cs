namespace LIAnsureProtect.Platform.Abstractions.DomainEvents;

/// <summary>
/// Implemented by aggregates that record domain events while handling a request.
/// The Platform <c>ModuleDbContext</c> base collects these on save, writes them to
/// the outbox in the same transaction, then clears them.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
