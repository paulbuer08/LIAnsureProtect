using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Platform.Abstractions.Outbox;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox.Consumers;

public sealed class ReferralOperationOutboxMessageConsumer(
    IReferralOperationProjector referralOperationProjector) : IOutboxMessageConsumer
{
    public async Task<OutboxMessageConsumerResult> ConsumeAsync(
        IOutboxMessageView outboxMessage,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var referralEvent = OutboxReferralOperationMapper.TryMap(outboxMessage);
        if (referralEvent is null)
            return OutboxMessageConsumerResult.NotHandled();

        await referralOperationProjector.ProjectAsync(referralEvent, cancellationToken);
        return OutboxMessageConsumerResult.Succeeded();
    }
}
