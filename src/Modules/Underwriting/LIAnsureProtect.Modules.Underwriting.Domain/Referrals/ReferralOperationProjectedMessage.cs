namespace LIAnsureProtect.Modules.Underwriting.Domain.Referrals;

/// <summary>
/// Idempotency marker: one row per source outbox-message id the projector has applied. Lets the
/// at-least-once dispatcher re-deliver safely (close/evidence projections append timeline entries and
/// are not naturally idempotent).
/// </summary>
public sealed class ReferralOperationProjectedMessage
{
    private ReferralOperationProjectedMessage(Guid sourceOutboxMessageId, DateTime appliedAtUtc)
    {
        SourceOutboxMessageId = sourceOutboxMessageId;
        AppliedAtUtc = appliedAtUtc;
    }

    private ReferralOperationProjectedMessage()
    {
    }

    public Guid SourceOutboxMessageId { get; private set; }

    public DateTime AppliedAtUtc { get; private set; }

    public static ReferralOperationProjectedMessage Record(Guid sourceOutboxMessageId, DateTime appliedAtUtc)
        => new(sourceOutboxMessageId, appliedAtUtc);
}
