using LIAnsureProtect.Platform.Abstractions.DomainEvents;

namespace LIAnsureProtect.Domain.Quotes;

public sealed record QuoteUnderwritingDecisionRecordedDomainEvent(
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    string ReviewedByUserId,
    QuoteUnderwritingDecision Decision,
    DateTime OccurredAtUtc) : IDomainEvent;
