namespace LIAnsureProtect.Platform.Abstractions.DomainEvents;

/// <summary>
/// A domain event: a record that something meaningful happened inside a bounded context.
/// Lives in the Platform shared kernel because the outbox capture mechanism
/// (<c>ModuleDbContext</c>) and every module's events depend on this contract.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}
