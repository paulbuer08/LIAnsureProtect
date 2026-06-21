using LIAnsureProtect.Application.Quotes.RatingProviders;

namespace LIAnsureProtect.Application.Quotes.Commands.CreateQuote;

public sealed record RatingProviderIndicationResult(
    string ProviderName,
    string Status,
    string MarketDisposition,
    string? ProviderReference,
    string? ProviderQuoteNumber,
    decimal? IndicatedPremium,
    decimal? IndicatedLimit,
    decimal? IndicatedRetention,
    int? HttpStatusCode,
    string FailureCategory,
    string? FailureReason,
    int AttemptCount,
    long DurationMs)
{
    public static RatingProviderIndicationResult FromProviderResult(RatingProviderResult result)
    {
        return new RatingProviderIndicationResult(
            result.ProviderName,
            result.Status.ToString(),
            result.MarketDisposition.ToString(),
            result.ProviderReference,
            result.ProviderQuoteNumber,
            result.IndicatedPremium,
            result.IndicatedLimit,
            result.IndicatedRetention,
            result.HttpStatusCode,
            result.FailureCategory.ToString(),
            result.FailureReason,
            result.AttemptCount,
            Convert.ToInt64(result.Duration.TotalMilliseconds));
    }
}
