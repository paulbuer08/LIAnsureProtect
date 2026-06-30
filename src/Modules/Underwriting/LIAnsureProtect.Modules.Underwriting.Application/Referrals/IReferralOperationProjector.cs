namespace LIAnsureProtect.Modules.Underwriting.Application.Referrals;

/// <summary>
/// Inbound port the outbox dispatcher calls to project a Quoting-context domain event onto the referral
/// operation aggregate. Implementations MUST be idempotent on <see cref="ReferralOperationEvent.SourceOutboxMessageId"/>
/// (the dispatcher delivers at-least-once). Create is additionally create-if-missing so a referred quote's
/// operation appears with no user-visible gap.
/// </summary>
public interface IReferralOperationProjector
{
    Task ProjectAsync(ReferralOperationEvent referralEvent, CancellationToken cancellationToken);
}

public enum ReferralOperationEventKind
{
    Created,
    DecisionRecorded,
    EvidenceRequestCreated,
    EvidenceRequestResponded,
    EvidenceRequestAccepted,
    EvidenceRequestReviewDecisionRecorded,
    EvidenceRequestCancelled,
    EvidenceRequestFollowUpSent
}

/// <summary>
/// Context-neutral projection event mapped from an outbox message on the legacy side (so the module never
/// references the legacy outbox or Quoting events). All cross-context values are primitives.
/// </summary>
public sealed record ReferralOperationEvent(
    Guid SourceOutboxMessageId,
    ReferralOperationEventKind Kind,
    Guid QuoteId,
    string ActorUserId,
    DateTime OccurredAtUtc,
    Guid? EvidenceRequestId,
    string? Decision);
