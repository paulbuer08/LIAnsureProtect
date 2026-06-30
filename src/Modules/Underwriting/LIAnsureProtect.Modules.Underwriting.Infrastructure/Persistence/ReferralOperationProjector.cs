using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

/// <summary>
/// Projects Quoting-context events onto the referral operation aggregate. Idempotent on the source
/// outbox-message id; create is additionally create-if-missing so a referred quote's operation appears
/// with no user-visible gap, and close/evidence self-heal by ensuring the operation exists first.
/// </summary>
public sealed class ReferralOperationProjector(
    UnderwritingDbContext dbContext,
    IUnderwritingQuoteContextReader quoteContextReader) : IReferralOperationProjector
{
    private const string SystemUserId = "system";

    public async Task ProjectAsync(ReferralOperationEvent referralEvent, CancellationToken cancellationToken)
    {
        var alreadyApplied = await dbContext.ReferralOperationProjectedMessages
            .AnyAsync(message => message.SourceOutboxMessageId == referralEvent.SourceOutboxMessageId, cancellationToken);
        if (alreadyApplied)
            return;

        var operation = await EnsureOperationAsync(referralEvent.QuoteId, cancellationToken);
        if (operation is null)
            return; // quote facts not yet readable; the dispatcher retries this message later.

        Apply(referralEvent, operation);

        dbContext.ReferralOperationProjectedMessages.Add(
            ReferralOperationProjectedMessage.Record(referralEvent.SourceOutboxMessageId, DateTime.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<QuoteReferralOperation?> EnsureOperationAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        var operation = await dbContext.QuoteReferralOperations
            .Include(candidate => candidate.Notes)
            .Include(candidate => candidate.Tasks)
            .Include(candidate => candidate.TimelineEntries)
            .SingleOrDefaultAsync(candidate => candidate.QuoteId == quoteId, cancellationToken);
        if (operation is not null)
            return operation;

        var quote = await quoteContextReader.GetForReferralOperationAsync(quoteId, cancellationToken);
        if (quote is null)
            return null;

        operation = QuoteReferralOperation.CreateDefault(
            quote.QuoteId, quote.RiskTier, quote.ReferredAtUtc, quote.ExpiresAtUtc);
        await dbContext.QuoteReferralOperations.AddAsync(operation, cancellationToken);
        return operation;
    }

    private static void Apply(ReferralOperationEvent referralEvent, QuoteReferralOperation operation)
    {
        var actor = string.IsNullOrWhiteSpace(referralEvent.ActorUserId) ? SystemUserId : referralEvent.ActorUserId;
        var at = referralEvent.OccurredAtUtc;
        var evidenceId = referralEvent.EvidenceRequestId ?? Guid.Empty;

        switch (referralEvent.Kind)
        {
            case ReferralOperationEventKind.Created:
                break; // EnsureOperationAsync already created it.
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
                operation.RecordEvidenceRequestAccepted(evidenceId, actor, at);
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
