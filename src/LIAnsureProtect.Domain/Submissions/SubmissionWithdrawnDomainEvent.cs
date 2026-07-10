using LIAnsureProtect.Platform.Abstractions.DomainEvents;

namespace LIAnsureProtect.Domain.Submissions;

public sealed record SubmissionWithdrawnDomainEvent(
    Guid SubmissionId,
    string OwnerUserId,
    DateTime OccurredAtUtc) : IDomainEvent;
