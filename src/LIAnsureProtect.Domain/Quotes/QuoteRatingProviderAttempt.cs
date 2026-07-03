namespace LIAnsureProtect.Domain.Quotes;

public sealed class QuoteRatingProviderAttempt
{
    // The only constructor: EF Core materializes through it, and the Record factory assigns
    // state via the private property setters — no parameter-heavy constructor to maintain.
    private QuoteRatingProviderAttempt()
    {
        ProviderName = string.Empty;
        RequestPayloadHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid QuoteId { get; private set; }

    public Quote Quote { get; private set; } = null!;

    public string ProviderName { get; private set; }

    public RatingProviderAttemptStatus Status { get; private set; }

    public RatingProviderMarketDisposition MarketDisposition { get; private set; }

    public string? ProviderReference { get; private set; }

    public string? ProviderQuoteNumber { get; private set; }

    public decimal? IndicatedPremium { get; private set; }

    public decimal? IndicatedLimit { get; private set; }

    public decimal? IndicatedRetention { get; private set; }

    public int? HttpStatusCode { get; private set; }

    public RatingProviderFailureCategory FailureCategory { get; private set; }

    public string? FailureReason { get; private set; }

    public int AttemptCount { get; private set; }

    public long DurationMs { get; private set; }

    public string RequestPayloadHash { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime CompletedAtUtc { get; private set; }

    public static QuoteRatingProviderAttempt Record(
        Guid quoteId,
        string providerName,
        RatingProviderAttemptStatus status,
        RatingProviderMarketDisposition marketDisposition,
        string? providerReference,
        string? providerQuoteNumber,
        decimal? indicatedPremium,
        decimal? indicatedLimit,
        decimal? indicatedRetention,
        int? httpStatusCode,
        RatingProviderFailureCategory failureCategory,
        string? failureReason,
        int attemptCount,
        TimeSpan duration,
        string requestPayloadHash,
        DateTime createdAtUtc,
        DateTime completedAtUtc)
    {
        if (quoteId == Guid.Empty)
            throw new ArgumentException("Quote id is required.", nameof(quoteId));

        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name is required.", nameof(providerName));

        if (attemptCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(attemptCount), "Attempt count must be greater than zero.");

        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot be negative.");

        if (string.IsNullOrWhiteSpace(requestPayloadHash))
            throw new ArgumentException("Request payload hash is required.", nameof(requestPayloadHash));

        return new QuoteRatingProviderAttempt
        {
            Id = Guid.NewGuid(),
            QuoteId = quoteId,
            ProviderName = providerName.Trim(),
            Status = status,
            MarketDisposition = marketDisposition,
            ProviderReference = Normalize(providerReference),
            ProviderQuoteNumber = Normalize(providerQuoteNumber),
            IndicatedPremium = indicatedPremium,
            IndicatedLimit = indicatedLimit,
            IndicatedRetention = indicatedRetention,
            HttpStatusCode = httpStatusCode,
            FailureCategory = failureCategory,
            FailureReason = Normalize(failureReason),
            AttemptCount = attemptCount,
            DurationMs = Convert.ToInt64(duration.TotalMilliseconds),
            RequestPayloadHash = requestPayloadHash.Trim(),
            CreatedAtUtc = createdAtUtc,
            CompletedAtUtc = completedAtUtc
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
