using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

/// <summary>
/// Projects Quoting-context events onto the referral operation aggregate. Idempotent on the source
/// outbox-message id; create is create-if-missing (shared with the write-command self-heal via the
/// repository) so a referred quote's operation appears with no user-visible gap.
/// </summary>
public sealed class ReferralOperationProjector(
    UnderwritingDbContext dbContext,
    IReferralOperationRepository operations) : IReferralOperationProjector
{
    private const string SystemUserId = "system";

    public async Task ProjectAsync(ReferralOperationEvent referralEvent, CancellationToken cancellationToken)
    {
        var alreadyApplied = await dbContext.ReferralOperationProjectedMessages
            .AnyAsync(message => message.SourceOutboxMessageId == referralEvent.SourceOutboxMessageId, cancellationToken);
        if (alreadyApplied)
            return;

        // The quote is always committed before its outbox message is dispatched, so the read-back used by
        // EnsureExistsForQuoteAsync is consistent. A null here means the quote does not exist (impossible
        // for a real referral event), so there is nothing to apply and nothing to mark.
        var operation = await operations.EnsureExistsForQuoteAsync(referralEvent.QuoteId, cancellationToken);
        if (operation is null)
            return;

        Apply(referralEvent, operation);

        dbContext.ReferralOperationProjectedMessages.Add(
            ReferralOperationProjectedMessage.Record(referralEvent.SourceOutboxMessageId, DateTime.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(ReferralOperationEvent referralEvent, QuoteReferralOperation operation)
    {
        var actor = string.IsNullOrWhiteSpace(referralEvent.ActorUserId) ? SystemUserId : referralEvent.ActorUserId;
        var at = referralEvent.OccurredAtUtc;
        var evidenceId = referralEvent.EvidenceRequestId ?? Guid.Empty;

        // A closed operation accepts no further mutations (EnsureOpen throws). Ordered outbox delivery
        // makes evidence-after-close impossible in practice, but guard anyway so a late or duplicate event
        // no-ops (and is still marked applied) instead of throwing and poisoning the shared dispatch loop.
        if (referralEvent.Kind != ReferralOperationEventKind.Created
            && operation.Status == ReferralOperationStatus.Closed)
            return;

        switch (referralEvent.Kind)
        {
            case ReferralOperationEventKind.Created:
                break; // EnsureExistsForQuoteAsync already created it.
            case ReferralOperationEventKind.DecisionRecorded:
                operation.CloseForDecision(actor, referralEvent.Decision ?? string.Empty, at);
                break;
            case ReferralOperationEventKind.EvidenceRequestCreated:
                operation.RecordEvidenceRequestCreated(evidenceId, actor, at);
                break;
            case ReferralOperationEventKind.EvidenceRequestResponded:
                operation.RecordEvidenceRequestResponded(evidenceId, actor, at);
                break;
            case ReferralOperationEventKind.EvidenceRequestAccepted:
                // Mirror the legacy accept path, which recorded BOTH an acceptance entry and a Satisfied
                // review-decision entry.
                operation.RecordEvidenceRequestAccepted(evidenceId, actor, at);
                operation.RecordEvidenceRequestReviewDecision(evidenceId, "Satisfied", actor, at);
                break;
            case ReferralOperationEventKind.EvidenceRequestReviewDecisionRecorded:
                operation.RecordEvidenceRequestReviewDecision(evidenceId, referralEvent.Decision ?? string.Empty, actor, at);
                break;
            case ReferralOperationEventKind.EvidenceRequestCancelled:
                operation.RecordEvidenceRequestCancelled(evidenceId, actor, at);
                break;
            case ReferralOperationEventKind.EvidenceRequestFollowUpSent:
                operation.RecordEvidenceRequestFollowUpSent(evidenceId, actor, at);
                break;
        }
    }
}
