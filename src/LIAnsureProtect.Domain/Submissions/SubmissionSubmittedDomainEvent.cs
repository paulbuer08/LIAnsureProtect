using LIAnsureProtect.Domain.Common;

namespace LIAnsureProtect.Domain.Submissions;

public sealed record SubmissionSubmittedDomainEvent(
    Guid SubmissionId,
    string OwnerUserId,
    DateTime OccurredAtUtc) : IDomainEvent;
