using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes.RatingProviders;

public sealed record RatingProviderResult(
    string ProviderName,
    RatingProviderAttemptStatus Status,
    RatingProviderMarketDisposition MarketDisposition,
    string? ProviderReference,
    string? ProviderQuoteNumber,
    decimal? IndicatedPremium,
    decimal? IndicatedLimit,
    decimal? IndicatedRetention,
    int? HttpStatusCode,
    RatingProviderFailureCategory FailureCategory,
    string? FailureReason,
    int AttemptCount,
    TimeSpan Duration,
    DateTime CompletedAtUtc)
{
    public static RatingProviderResult Succeeded(
        string providerName,
        RatingProviderMarketDisposition marketDisposition,
        string? providerReference,
        string? providerQuoteNumber,
        decimal? indicatedPremium,
        decimal? indicatedLimit,
        decimal? indicatedRetention,
        int? httpStatusCode,
        int attemptCount,
        TimeSpan duration,
        DateTime completedAtUtc)
    {
        return new RatingProviderResult(
            providerName,
            RatingProviderAttemptStatus.Succeeded,
            marketDisposition,
            providerReference,
            providerQuoteNumber,
            indicatedPremium,
            indicatedLimit,
            indicatedRetention,
            httpStatusCode,
            RatingProviderFailureCategory.None,
            null,
            attemptCount,
            duration,
            completedAtUtc);
    }

    public static RatingProviderResult Failed(
        string providerName,
        RatingProviderAttemptStatus status,
        RatingProviderMarketDisposition marketDisposition,
        RatingProviderFailureCategory failureCategory,
        string failureReason,
        int? httpStatusCode,
        int attemptCount,
        TimeSpan duration,
        DateTime completedAtUtc)
    {
        return new RatingProviderResult(
            providerName,
            status,
            marketDisposition,
            null,
            null,
            null,
            null,
            null,
            httpStatusCode,
            failureCategory,
            failureReason,
            attemptCount,
            duration,
            completedAtUtc);
    }
}
