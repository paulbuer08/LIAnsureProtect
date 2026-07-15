using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Modules.Underwriting.Application.Assurance;
using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping.Assurance;

public sealed class QuoteGeneratedAssuranceMapper : IOutboxMessageMapper<QuoteAssuranceEvent>
{
    public string EventType => nameof(QuoteGeneratedDomainEvent);

    public QuoteAssuranceEvent Map(IOutboxMessageView outboxMessage)
    {
        var domainEvent = OutboxMessageJson.Deserialize<QuoteGeneratedDomainEvent>(outboxMessage);

        return new QuoteAssuranceEvent(
            outboxMessage.Id,
            domainEvent.QuoteId,
            domainEvent.SubmissionId,
            domainEvent.Version,
            domainEvent.SupersedesQuoteId,
            domainEvent.OccurredAtUtc);
    }
}
