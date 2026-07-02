using System.Text.Json;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using ModuleQuoteEvidenceRequestAcceptedDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestAcceptedDomainEvent;
using ModuleQuoteEvidenceRequestRemediationRequiredDomainEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestRemediationRequiredDomainEvent;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.ReferralOperations;

public sealed class QuoteGeneratedReferralOperationMapper : IOutboxMessageMapper<ReferralOperationEvent>
{
    public string EventType => nameof(QuoteGeneratedDomainEvent);

    public ReferralOperationEvent? Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<QuoteGeneratedDomainEvent>(outboxMessage);
        if (domainEvent.Status != QuoteStatus.Referred)
            return null;

        return new ReferralOperationEvent(
            outboxMessage.Id,
            ReferralOperationEventKind.Created,
            domainEvent.QuoteId,
            "system",
            domainEvent.OccurredAtUtc,
            null,
            null);
    }
}

public sealed class QuoteUnderwritingDecisionReferralOperationMapper
    : IOutboxMessageMapper<ReferralOperationEvent>
{
    public string EventType => nameof(QuoteUnderwritingDecisionRecordedDomainEvent);

    public ReferralOperationEvent Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<QuoteUnderwritingDecisionRecordedDomainEvent>(outboxMessage);

        return new ReferralOperationEvent(
            outboxMessage.Id,
            ReferralOperationEventKind.DecisionRecorded,
            domainEvent.QuoteId,
            domainEvent.ReviewedByUserId,
            domainEvent.OccurredAtUtc,
            null,
            domainEvent.Decision.ToString());
    }
}

public sealed class EvidenceRequestCreatedReferralOperationMapper
    : JsonReferralOperationMapper
{
    public EvidenceRequestCreatedReferralOperationMapper()
        : base("QuoteEvidenceRequestCreatedDomainEvent", ReferralOperationEventKind.EvidenceRequestCreated)
    {
    }
}

public sealed class EvidenceRequestRespondedReferralOperationMapper
    : JsonReferralOperationMapper
{
    public EvidenceRequestRespondedReferralOperationMapper()
        : base("QuoteEvidenceRequestRespondedDomainEvent", ReferralOperationEventKind.EvidenceRequestResponded)
    {
    }
}

public sealed class EvidenceRequestCancelledReferralOperationMapper
    : JsonReferralOperationMapper
{
    public EvidenceRequestCancelledReferralOperationMapper()
        : base("QuoteEvidenceRequestCancelledDomainEvent", ReferralOperationEventKind.EvidenceRequestCancelled)
    {
    }
}

public sealed class EvidenceRequestFollowUpSentReferralOperationMapper
    : JsonReferralOperationMapper
{
    public EvidenceRequestFollowUpSentReferralOperationMapper()
        : base("QuoteEvidenceRequestFollowUpSentDomainEvent", ReferralOperationEventKind.EvidenceRequestFollowUpSent)
    {
    }
}

public sealed class EvidenceRequestAcceptedReferralOperationMapper
    : IOutboxMessageMapper<ReferralOperationEvent>
{
    public string EventType => "QuoteEvidenceRequestAcceptedDomainEvent";

    public ReferralOperationEvent Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleQuoteEvidenceRequestAcceptedDomainEvent>(outboxMessage);

        return new ReferralOperationEvent(
            outboxMessage.Id,
            ReferralOperationEventKind.EvidenceRequestAccepted,
            domainEvent.QuoteId,
            domainEvent.AcceptedByUserId,
            domainEvent.OccurredAtUtc,
            domainEvent.EvidenceRequestId,
            null);
    }
}

public sealed class EvidenceRequestRemediationRequiredReferralOperationMapper
    : IOutboxMessageMapper<ReferralOperationEvent>
{
    public string EventType => "QuoteEvidenceRequestRemediationRequiredDomainEvent";

    public ReferralOperationEvent Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<ModuleQuoteEvidenceRequestRemediationRequiredDomainEvent>(
            outboxMessage);

        return new ReferralOperationEvent(
            outboxMessage.Id,
            ReferralOperationEventKind.EvidenceRequestReviewDecisionRecorded,
            domainEvent.QuoteId,
            domainEvent.ReviewedByUserId,
            domainEvent.OccurredAtUtc,
            domainEvent.EvidenceRequestId,
            domainEvent.Decision.ToString());
    }
}

public abstract class JsonReferralOperationMapper(
    string eventType,
    ReferralOperationEventKind kind) : IOutboxMessageMapper<ReferralOperationEvent>
{
    public string EventType { get; } = eventType;

    public ReferralOperationEvent Map(IOutboxMessageView outboxMessage)
    {
        using var document = JsonDocument.Parse(outboxMessage.Payload);
        var root = document.RootElement;
        var quoteId = root.GetProperty("QuoteId").GetGuid();
        var evidenceRequestId = root.GetProperty("EvidenceRequestId").GetGuid();
        var actor = ActorFor(kind, root);
        var occurredAtUtc = root.GetProperty("OccurredAtUtc").GetDateTime();

        return new ReferralOperationEvent(
            outboxMessage.Id,
            kind,
            quoteId,
            actor,
            occurredAtUtc,
            evidenceRequestId,
            null);
    }

    private static string ActorFor(ReferralOperationEventKind kind, JsonElement root) => kind switch
    {
        ReferralOperationEventKind.EvidenceRequestCreated => root.GetProperty("RequestedByUserId").GetString() ?? "system",
        ReferralOperationEventKind.EvidenceRequestResponded => root.GetProperty("RespondedByUserId").GetString() ?? "system",
        ReferralOperationEventKind.EvidenceRequestCancelled => root.GetProperty("CancelledByUserId").GetString() ?? "system",
        ReferralOperationEventKind.EvidenceRequestFollowUpSent => root.GetProperty("FollowedUpByUserId").GetString() ?? "system",
        _ => "system"
    };
}
