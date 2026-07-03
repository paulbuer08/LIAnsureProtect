namespace LIAnsureProtect.Domain.Quotes;

public sealed class QuoteUnderwritingReview
{
    // The only constructor: EF Core materializes through it, and the Record factory assigns
    // state via the private property setters — no parameter-heavy constructor to maintain.
    private QuoteUnderwritingReview()
    {
        ReviewedByUserId = string.Empty;
        Reason = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid QuoteId { get; private set; }

    public QuoteUnderwritingDecision Decision { get; private set; }

    public string ReviewedByUserId { get; private set; }

    public string Reason { get; private set; }

    public string? Notes { get; private set; }

    public decimal PremiumBefore { get; private set; }

    public decimal PremiumAfter { get; private set; }

    public decimal RetentionBefore { get; private set; }

    public decimal RetentionAfter { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static QuoteUnderwritingReview Record(
        Guid quoteId,
        QuoteUnderwritingDecision decision,
        string reviewedByUserId,
        string reason,
        string? notes,
        decimal premiumBefore,
        decimal premiumAfter,
        decimal retentionBefore,
        decimal retentionAfter,
        DateTime createdAtUtc)
    {
        if (quoteId == Guid.Empty)
            throw new ArgumentException("Quote id is required.", nameof(quoteId));

        if (string.IsNullOrWhiteSpace(reviewedByUserId))
            throw new ArgumentException("Reviewed by user id is required.", nameof(reviewedByUserId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Review reason is required.", nameof(reason));

        return new QuoteUnderwritingReview
        {
            Id = Guid.NewGuid(),
            QuoteId = quoteId,
            Decision = decision,
            ReviewedByUserId = reviewedByUserId,
            Reason = reason.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            PremiumBefore = premiumBefore,
            PremiumAfter = premiumAfter,
            RetentionBefore = retentionBefore,
            RetentionAfter = retentionAfter,
            CreatedAtUtc = createdAtUtc
        };
    }
}
