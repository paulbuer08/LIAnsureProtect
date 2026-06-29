using LIAnsureProtect.Platform.Abstractions.DomainEvents;

namespace LIAnsureProtect.Domain.Submissions;

public sealed record SubmissionSubmittedDomainEvent(
    Guid SubmissionId,
    string OwnerUserId,
    DateTime OccurredAtUtc) : IDomainEvent;
