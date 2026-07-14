using LIAnsureProtect.Platform.Abstractions.DomainEvents;

namespace LIAnsureProtect.Domain.Quotes;

public sealed record QuoteGeneratedDomainEvent(
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    QuoteStatus Status,
    DateTime OccurredAtUtc,
    int Version = 1,
    decimal Premium = 0,
    DateTime? ExpiresAtUtc = null) : IDomainEvent;
