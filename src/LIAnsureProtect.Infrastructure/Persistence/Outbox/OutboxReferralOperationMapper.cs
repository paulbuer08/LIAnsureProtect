using System.Text.Json;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using ModuleQuoteEvidenceRequestAcceptedDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestAcceptedDomainEvent;
using ModuleQuoteEvidenceRequestRemediationRequiredDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestRemediationRequiredDomainEvent;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

/// <summary>
/// Legacy-side mapper from an outbox message (a serialized Quoting domain event) to the module's
/// context-neutral <see cref="ReferralOperationEvent"/>. Returns null for events the referral operation
/// does not react to.
/// </summary>
internal static class OutboxReferralOperationMapper
{
    private const string QuoteEvidenceRequestCreatedDomainEventType = "QuoteEvidenceRequestCreatedDomainEvent";
    private const string QuoteEvidenceRequestRespondedDomainEventType = "QuoteEvidenceRequestRespondedDomainEvent";
    private const string QuoteEvidenceRequestAcceptedDomainEventType = "QuoteEvidenceRequestAcceptedDomainEvent";
    private const string QuoteEvidenceRequestCancelledDomainEventType = "QuoteEvidenceRequestCancelledDomainEvent";
    private const string QuoteEvidenceRequestFollowUpSentDomainEventType = "QuoteEvidenceRequestFollowUpSentDomainEvent";
    private const string QuoteEvidenceRequestRemediationRequiredDomainEventType = "QuoteEvidenceRequestRemediationRequiredDomainEvent";

    public static ReferralOperationEvent? TryMap(IOutboxMessageView outboxMessage)
    {
        return outboxMessage.Type switch
        {
            nameof(QuoteGeneratedDomainEvent) => MapGenerated(outboxMessage),
            nameof(QuoteUnderwritingDecisionRecordedDomainEvent) => MapDecision(outboxMessage),
            QuoteEvidenceRequestCreatedDomainEventType => MapEvidence(outboxMessage, ReferralOperationEventKind.EvidenceRequestCreated),
            QuoteEvidenceRequestRespondedDomainEventType => MapEvidence(outboxMessage, ReferralOperationEventKind.EvidenceRequestResponded),
            QuoteEvidenceRequestAcceptedDomainEventType => MapEvidenceAccepted(outboxMessage),
            QuoteEvidenceRequestCancelledDomainEventType => MapEvidence(outboxMessage, ReferralOperationEventKind.EvidenceRequestCancelled),
            QuoteEvidenceRequestFollowUpSentDomainEventType => MapEvidence(outboxMessage, ReferralOperationEventKind.EvidenceRequestFollowUpSent),
            QuoteEvidenceRequestRemediationRequiredDomainEventType => MapRemediation(outboxMessage),
            _ => null
        };
    }

    private static ReferralOperationEvent? MapGenerated(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<QuoteGeneratedDomainEvent>(outboxMessage);
        if (domainEvent.Status != QuoteStatus.Referred)
            return null;

        return new ReferralOperationEvent(
            outboxMessage.Id, ReferralOperationEventKind.Created, domainEvent.QuoteId,
            "system", domainEvent.OccurredAtUtc, null, null);
    }

    private static ReferralOperationEvent MapDecision(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<QuoteUnderwritingDecisionRecordedDomainEvent>(outboxMessage);
        return new ReferralOperationEvent(
            outboxMessage.Id, ReferralOperationEventKind.DecisionRecorded, domainEvent.QuoteId,
            domainEvent.ReviewedByUserId, domainEvent.OccurredAtUtc, null, domainEvent.Decision.ToString());
    }

    private static ReferralOperationEvent MapEvidence(IOutboxMessageView outboxMessage, ReferralOperationEventKind kind)
    {
        // QuoteEvidenceRequestCreated/Responded/Cancelled/FollowUpSent share the same shape we need:
        // EvidenceRequestId, QuoteId, an actor user id, OccurredAtUtc.
        using var document = JsonDocument.Parse(outboxMessage.Payload);
        var root = document.RootElement;
        var quoteId = root.GetProperty("QuoteId").GetGuid();
        var evidenceRequestId = root.GetProperty("EvidenceRequestId").GetGuid();
        var actor = ActorFor(kind, root);
        var occurredAtUtc = root.GetProperty("OccurredAtUtc").GetDateTime();

        return new ReferralOperationEvent(
            outboxMessage.Id, kind, quoteId, actor, occurredAtUtc, evidenceRequestId, null);
    }

    private static ReferralOperationEvent MapEvidenceAccepted(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<ModuleQuoteEvidenceRequestAcceptedDomainEvent>(outboxMessage);
        return new ReferralOperationEvent(
            outboxMessage.Id, ReferralOperationEventKind.EvidenceRequestAccepted, domainEvent.QuoteId,
            domainEvent.AcceptedByUserId, domainEvent.OccurredAtUtc, domainEvent.EvidenceRequestId, null);
    }

    private static ReferralOperationEvent MapRemediation(IOutboxMessageView outboxMessage)
    {
        var domainEvent = Deserialize<ModuleQuoteEvidenceRequestRemediationRequiredDomainEvent>(outboxMessage);
        return new ReferralOperationEvent(
            outboxMessage.Id, ReferralOperationEventKind.EvidenceRequestReviewDecisionRecorded, domainEvent.QuoteId,
            domainEvent.ReviewedByUserId, domainEvent.OccurredAtUtc, domainEvent.EvidenceRequestId,
            domainEvent.Decision.ToString());
    }

    private static string ActorFor(ReferralOperationEventKind kind, JsonElement root) => kind switch
    {
        ReferralOperationEventKind.EvidenceRequestCreated => root.GetProperty("RequestedByUserId").GetString() ?? "system",
        ReferralOperationEventKind.EvidenceRequestResponded => root.GetProperty("RespondedByUserId").GetString() ?? "system",
        ReferralOperationEventKind.EvidenceRequestCancelled => root.GetProperty("CancelledByUserId").GetString() ?? "system",
        ReferralOperationEventKind.EvidenceRequestFollowUpSent => root.GetProperty("FollowedUpByUserId").GetString() ?? "system",
        _ => "system"
    };

    private static T Deserialize<T>(IOutboxMessageView outboxMessage)
        => JsonSerializer.Deserialize<T>(outboxMessage.Payload)
            ?? throw new InvalidOperationException($"Outbox message {outboxMessage.Id} payload could not be deserialized.");
}
