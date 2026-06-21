using LIAnsureProtect.Domain.Common;

namespace LIAnsureProtect.Domain.Policies;

public sealed record PolicyBoundDomainEvent(
    Guid PolicyId,
    string PolicyNumber,
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    string BoundByUserId,
    DateTime OccurredAtUtc) : IDomainEvent;
