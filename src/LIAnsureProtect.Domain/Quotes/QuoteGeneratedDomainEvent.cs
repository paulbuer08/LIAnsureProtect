using LIAnsureProtect.Domain.Common;

namespace LIAnsureProtect.Domain.Quotes;

public sealed record QuoteGeneratedDomainEvent(
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    QuoteStatus Status,
    DateTime OccurredAtUtc) : IDomainEvent;
