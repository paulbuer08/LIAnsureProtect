namespace LIAnsureProtect.Domain.Quotes;

public sealed class QuoteRatingProviderAttempt
{
    private QuoteRatingProviderAttempt(
        Guid id,
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
        long durationMs,
        string requestPayloadHash,
        DateTime createdAtUtc,
        DateTime completedAtUtc)
    {
        Id = id;
        QuoteId = quoteId;
        ProviderName = providerName;
        Status = status;
        MarketDisposition = marketDisposition;
        ProviderReference = providerReference;
        ProviderQuoteNumber = providerQuoteNumber;
        IndicatedPremium = indicatedPremium;
        IndicatedLimit = indicatedLimit;
        IndicatedRetention = indicatedRetention;
        HttpStatusCode = httpStatusCode;
        FailureCategory = failureCategory;
        FailureReason = failureReason;
        AttemptCount = attemptCount;
        DurationMs = durationMs;
        RequestPayloadHash = requestPayloadHash;
        CreatedAtUtc = createdAtUtc;
        CompletedAtUtc = completedAtUtc;
    }

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

        return new QuoteRatingProviderAttempt(
            Guid.NewGuid(),
            quoteId,
            providerName.Trim(),
            status,
            marketDisposition,
            Normalize(providerReference),
            Normalize(providerQuoteNumber),
            indicatedPremium,
            indicatedLimit,
            indicatedRetention,
            httpStatusCode,
            failureCategory,
            Normalize(failureReason),
            attemptCount,
            Convert.ToInt64(duration.TotalMilliseconds),
            requestPayloadHash.Trim(),
            createdAtUtc,
            completedAtUtc);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
