using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping;
using LIAnsureProtect.Modules.Underwriting.Application.Assurance;
using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Consumers;

public sealed class QuoteAssuranceOutboxMessageConsumer(
    OutboxMessageMapperRegistry<QuoteAssuranceEvent> registry,
    IQuoteAssuranceProjector projector) : IOutboxMessageConsumer
{
    public async Task<OutboxMessageConsumerResult> ConsumeAsync(
        IOutboxMessageView outboxMessage,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (!registry.TryMap(outboxMessage, out var assuranceEvent) || assuranceEvent is null)
            return OutboxMessageConsumerResult.NotHandled();

        await projector.ProjectAsync(assuranceEvent, cancellationToken);
        return OutboxMessageConsumerResult.Succeeded();
    }
}
