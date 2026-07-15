using LIAnsureProtect.Platform.Abstractions.DomainEvents;

namespace LIAnsureProtect.Domain.Quotes;

public sealed record ReassessmentReviewRequestedDomainEvent(
    Guid ReassessmentRequestId,
    Guid SubmissionId,
    Guid BaseQuoteId,
    int BaseQuoteVersion,
    string OwnerUserId,
    DateTime OccurredAtUtc,
    string? SubmissionReference = null,
    string? CompanyName = null) : IDomainEvent;

public sealed record ReassessmentReviewDecisionRecordedDomainEvent(
    Guid ReassessmentRequestId,
    Guid SubmissionId,
    Guid BaseQuoteId,
    int BaseQuoteVersion,
    Guid? CreatedQuoteId,
    string OwnerUserId,
    string Status,
    string DecisionReason,
    DateTime OccurredAtUtc,
    string? SubmissionReference = null,
    string? CompanyName = null) : IDomainEvent;
