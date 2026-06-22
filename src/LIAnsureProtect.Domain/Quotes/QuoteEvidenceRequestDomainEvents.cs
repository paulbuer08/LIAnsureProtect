using LIAnsureProtect.Domain.Common;

namespace LIAnsureProtect.Domain.Quotes;

public sealed record QuoteEvidenceRequestCreatedDomainEvent(
    Guid EvidenceRequestId,
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    string RequestedByUserId,
    EvidenceRequestCategory Category,
    DateTime DueAtUtc,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record QuoteEvidenceRequestRespondedDomainEvent(
    Guid EvidenceRequestId,
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    string RequestedByUserId,
    string RespondedByUserId,
    EvidenceRequestCategory Category,
    DateTime DueAtUtc,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record QuoteEvidenceRequestAcceptedDomainEvent(
    Guid EvidenceRequestId,
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    string RequestedByUserId,
    string AcceptedByUserId,
    EvidenceRequestCategory Category,
    DateTime DueAtUtc,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record QuoteEvidenceRequestCancelledDomainEvent(
    Guid EvidenceRequestId,
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    string RequestedByUserId,
    string CancelledByUserId,
    EvidenceRequestCategory Category,
    DateTime DueAtUtc,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record QuoteEvidenceRequestFollowUpSentDomainEvent(
    Guid EvidenceRequestId,
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    string RequestedByUserId,
    string FollowedUpByUserId,
    EvidenceRequestCategory Category,
    DateTime DueAtUtc,
    DateTime OccurredAtUtc) : IDomainEvent;
