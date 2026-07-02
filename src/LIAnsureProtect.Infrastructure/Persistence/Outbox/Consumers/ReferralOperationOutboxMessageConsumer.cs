using LIAnsureProtect.Infrastructure.Persistence.Outbox.Mapping;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Consumers;

public sealed class ReferralOperationOutboxMessageConsumer(
    OutboxMessageMapperRegistry<ReferralOperationEvent> registry,
    IReferralOperationProjector referralOperationProjector) : IOutboxMessageConsumer
{
    public async Task<OutboxMessageConsumerResult> ConsumeAsync(
        IOutboxMessageView outboxMessage,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (!registry.TryMap(outboxMessage, out var referralEvent) || referralEvent is null)
            return OutboxMessageConsumerResult.NotHandled();

        await referralOperationProjector.ProjectAsync(referralEvent, cancellationToken);
        return OutboxMessageConsumerResult.Succeeded();
    }
}
