using LIAnsureProtect.Application.Quotes.Assurance;
using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping;
using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Consumers;

public sealed class QuoteAssuranceDecisionOutboxMessageConsumer(
    OutboxMessageMapperRegistry<QuoteAssuranceDecisionEvent> registry,
    IQuoteAssuranceDecisionProjector projector) : IOutboxMessageConsumer
{
    public async Task<OutboxMessageConsumerResult> ConsumeAsync(
        IOutboxMessageView outboxMessage,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (!registry.TryMap(outboxMessage, out var decisionEvent) || decisionEvent is null)
            return OutboxMessageConsumerResult.NotHandled();

        await projector.ProjectAsync(decisionEvent, cancellationToken);
        return OutboxMessageConsumerResult.Succeeded();
    }
}
