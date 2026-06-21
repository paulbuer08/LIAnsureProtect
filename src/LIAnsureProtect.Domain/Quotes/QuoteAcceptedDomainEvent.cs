using LIAnsureProtect.Domain.Common;

namespace LIAnsureProtect.Domain.Quotes;

public sealed record QuoteAcceptedDomainEvent(
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    string AcceptedByUserId,
    DateTime OccurredAtUtc) : IDomainEvent;
