namespace LIAnsureProtect.Infrastructure.Persistence;

public sealed class QuoteAssuranceDecisionProjectedMessage
{
    private QuoteAssuranceDecisionProjectedMessage()
    {
    }

    public Guid SourceOutboxMessageId { get; private set; }

    public DateTime ProjectedAtUtc { get; private set; }

    public static QuoteAssuranceDecisionProjectedMessage Record(Guid sourceOutboxMessageId, DateTime projectedAtUtc)
    {
        if (sourceOutboxMessageId == Guid.Empty)
            throw new ArgumentException("Source outbox message id is required.", nameof(sourceOutboxMessageId));

        return new QuoteAssuranceDecisionProjectedMessage
        {
            SourceOutboxMessageId = sourceOutboxMessageId,
            ProjectedAtUtc = projectedAtUtc
        };
    }
}
