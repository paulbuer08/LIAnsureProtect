using LIAnsureProtect.Application.Quotes.Assurance;
using LIAnsureProtect.Platform.Abstractions.Outbox;
using AcceptedEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestAcceptedDomainEvent;
using RemediationEvent = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.QuoteEvidenceRequestRemediationRequiredDomainEvent;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.Assurance;

public sealed class EvidenceAcceptedAssuranceDecisionMapper
    : IOutboxMessageMapper<QuoteAssuranceDecisionEvent>
{
    public string EventType => nameof(AcceptedEvent);

    public QuoteAssuranceDecisionEvent Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<AcceptedEvent>(outboxMessage);

        return new QuoteAssuranceDecisionEvent(
            outboxMessage.Id,
            domainEvent.QuoteId,
            domainEvent.Category.ToString(),
            true,
            domainEvent.AcceptedByUserId,
            domainEvent.OccurredAtUtc);
    }
}

public sealed class EvidenceRemediationAssuranceDecisionMapper
    : IOutboxMessageMapper<QuoteAssuranceDecisionEvent>
{
    public string EventType => nameof(RemediationEvent);

    public QuoteAssuranceDecisionEvent Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<RemediationEvent>(outboxMessage);

        return new QuoteAssuranceDecisionEvent(
            outboxMessage.Id,
            domainEvent.QuoteId,
            domainEvent.Category.ToString(),
            false,
            domainEvent.ReviewedByUserId,
            domainEvent.OccurredAtUtc);
    }
}
