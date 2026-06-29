using LIAnsureProtect.Platform.Abstractions.DomainEvents;

namespace LIAnsureProtect.Domain.Policies;

public sealed record PolicyBoundDomainEvent(
    Guid PolicyId,
    string PolicyNumber,
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    string BoundByUserId,
    DateTime OccurredAtUtc) : IDomainEvent;
