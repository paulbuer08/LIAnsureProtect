namespace LIAnsureProtect.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}
