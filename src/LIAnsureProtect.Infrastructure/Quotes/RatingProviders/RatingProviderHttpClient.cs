using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LIAnsureProtect.Application.Quotes.RatingProviders;
using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Infrastructure.Quotes.RatingProviders;

internal sealed class RatingProviderHttpClient(HttpClient httpClient) : IRatingProviderClient
{
    private const string ProviderName = "Contoso Specialty";

    public async Task<RatingProviderResult> GetMarketIndicationAsync(
        RatingProviderRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var attemptCounter = new RatingProviderAttemptCounter();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "market-indications")
        {
            Content = JsonContent.Create(ProviderMarketIndicationRequest.FromApplicationRequest(request))
        };
        httpRequest.Options.Set(RatingProviderAttemptCounter.OptionsKey, attemptCounter);

        try
        {
            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            stopwatch.Stop();
            var completedAtUtc = DateTime.UtcNow;
            var attemptCount = Math.Max(1, attemptCounter.Count);

            if (!response.IsSuccessStatusCode)
            {
                return CreateFailureResult(
                    response.StatusCode,
                    attemptCount,
                    stopwatch.Elapsed,
                    completedAtUtc);
            }

            var providerResponse = await response.Content.ReadFromJsonAsync<ProviderMarketIndicationResponse>(
                cancellationToken: cancellationToken);

            if (providerResponse is null)
            {
                return RatingProviderResult.Failed(
                    ProviderName,
                    RatingProviderAttemptStatus.Failed,
                    RatingProviderMarketDisposition.Unavailable,
                    RatingProviderFailureCategory.Unexpected,
                    "Provider returned an empty response.",
                    (int)response.StatusCode,
                    attemptCount,
                    stopwatch.Elapsed,
                    completedAtUtc);
            }

            return RatingProviderResult.Succeeded(
                ProviderName,
                providerResponse.MarketDisposition,
                providerResponse.ProviderReference,
                providerResponse.ProviderQuoteNumber,
                providerResponse.IndicatedPremium,
                providerResponse.IndicatedLimit,
                providerResponse.IndicatedRetention,
                (int)response.StatusCode,
                attemptCount,
                stopwatch.Elapsed,
                completedAtUtc);
        }
        catch (Exception exception) when (IsCircuitOpen(exception))
        {
            stopwatch.Stop();

            return RatingProviderResult.Failed(
                ProviderName,
                RatingProviderAttemptStatus.CircuitOpen,
                RatingProviderMarketDisposition.Unavailable,
                RatingProviderFailureCategory.CircuitOpen,
                "Provider circuit is open after repeated failures.",
                null,
                Math.Max(1, attemptCounter.Count),
                stopwatch.Elapsed,
                DateTime.UtcNow);
        }
        catch (Exception exception) when (IsTimeout(exception))
        {
            stopwatch.Stop();

            return RatingProviderResult.Failed(
                ProviderName,
                RatingProviderAttemptStatus.Unavailable,
                RatingProviderMarketDisposition.Unavailable,
                RatingProviderFailureCategory.Timeout,
                "Provider did not respond before the configured timeout.",
                null,
                Math.Max(1, attemptCounter.Count),
                stopwatch.Elapsed,
                DateTime.UtcNow);
        }
        catch
        {
            stopwatch.Stop();

            return RatingProviderResult.Failed(
                ProviderName,
                RatingProviderAttemptStatus.Unavailable,
                RatingProviderMarketDisposition.Unavailable,
                RatingProviderFailureCategory.Unexpected,
                "Provider call failed before a usable market indication was returned.",
                null,
                Math.Max(1, attemptCounter.Count),
                stopwatch.Elapsed,
                DateTime.UtcNow);
        }
    }

    private static RatingProviderResult CreateFailureResult(
        HttpStatusCode statusCode,
        int attemptCount,
        TimeSpan duration,
        DateTime completedAtUtc)
    {
        var failureCategory = statusCode switch
        {
            HttpStatusCode.TooManyRequests => RatingProviderFailureCategory.RateLimited,
            >= HttpStatusCode.InternalServerError => RatingProviderFailureCategory.ServerError,
            >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError =>
                RatingProviderFailureCategory.ValidationRejected,
            _ => RatingProviderFailureCategory.Unexpected
        };

        var attemptStatus = statusCode == HttpStatusCode.TooManyRequests
            ? RatingProviderAttemptStatus.Unavailable
            : RatingProviderAttemptStatus.Failed;

        return RatingProviderResult.Failed(
            ProviderName,
            attemptStatus,
            RatingProviderMarketDisposition.Unavailable,
            failureCategory,
            "Provider returned a non-success response.",
            (int)statusCode,
            attemptCount,
            duration,
            completedAtUtc);
    }

    private static bool IsCircuitOpen(Exception exception)
    {
        return exception.GetType().Name.Contains("BrokenCircuit", StringComparison.Ordinal);
    }

    private static bool IsTimeout(Exception exception)
    {
        return exception is TaskCanceledException
            || exception.GetType().Name.Contains("Timeout", StringComparison.Ordinal);
    }

    private sealed record ProviderMarketIndicationRequest(
        Guid QuoteId,
        Guid SubmissionId,
        string OwnerUserId,
        string IndustryClass,
        string AnnualRevenueBand,
        decimal RequestedLimit,
        decimal Retention,
        string MfaStatus,
        string EdrStatus,
        string BackupMaturity,
        bool HasIncidentResponsePlan,
        int PriorCyberIncidents,
        string SensitiveDataExposure,
        decimal LocalPremium,
        string LocalRiskTier,
        string LocalStatus,
        string LocalRatingStrategyName)
    {
        public static ProviderMarketIndicationRequest FromApplicationRequest(RatingProviderRequest request)
        {
            return new ProviderMarketIndicationRequest(
                request.QuoteId,
                request.SubmissionId,
                request.OwnerUserId,
                request.IndustryClass.ToString(),
                request.AnnualRevenueBand.ToString(),
                request.RequestedLimit,
                request.Retention,
                request.MfaStatus.ToString(),
                request.EdrStatus.ToString(),
                request.BackupMaturity.ToString(),
                request.HasIncidentResponsePlan,
                request.PriorCyberIncidents,
                request.SensitiveDataExposure.ToString(),
                request.LocalPremium,
                request.LocalRiskTier.ToString(),
                request.LocalStatus.ToString(),
                request.LocalRatingStrategyName);
        }
    }

    private sealed record ProviderMarketIndicationResponse(
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        RatingProviderMarketDisposition MarketDisposition,
        string ProviderReference,
        string ProviderQuoteNumber,
        decimal? IndicatedPremium,
        decimal? IndicatedLimit,
        decimal? IndicatedRetention);
}
