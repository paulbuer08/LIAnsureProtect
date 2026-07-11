namespace LIAnsureProtect.Modules.Underwriting.Domain.Evidence;

public sealed class QuoteAssuranceProjectedMessage
{
    private QuoteAssuranceProjectedMessage()
    {
    }

    public Guid SourceOutboxMessageId { get; private set; }

    public DateTime ProjectedAtUtc { get; private set; }

    public static QuoteAssuranceProjectedMessage Record(Guid sourceOutboxMessageId, DateTime projectedAtUtc)
    {
        if (sourceOutboxMessageId == Guid.Empty)
            throw new ArgumentException("Source outbox message id is required.", nameof(sourceOutboxMessageId));

        return new QuoteAssuranceProjectedMessage
        {
            SourceOutboxMessageId = sourceOutboxMessageId,
            ProjectedAtUtc = projectedAtUtc
        };
    }
}
