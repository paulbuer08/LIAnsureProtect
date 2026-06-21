using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Infrastructure.Quotes.RatingProviders;

internal sealed class SimulatedRatingProviderHttpMessageHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Post
            || request.RequestUri?.AbsolutePath.Trim('/') != "market-indications")
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var providerRequest = await request.Content!.ReadFromJsonAsync<ProviderMarketIndicationRequest>(
            JsonOptions,
            cancellationToken);

        if (providerRequest is null)
            return new HttpResponseMessage(HttpStatusCode.BadRequest);

        var disposition = providerRequest.LocalStatus == nameof(QuoteStatus.Referred)
            ? RatingProviderMarketDisposition.Referred
            : RatingProviderMarketDisposition.Quoted;
        var response = new ProviderMarketIndicationResponse(
            disposition,
            $"CNT-REF-{providerRequest.QuoteId:N}"[..20],
            $"CNT-Q-{providerRequest.QuoteId:N}"[..18],
            decimal.Round(providerRequest.LocalPremium * 1.03m, 2),
            providerRequest.RequestedLimit,
            providerRequest.Retention);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(response, options: JsonOptions)
        };
    }

    private sealed record ProviderMarketIndicationRequest(
        Guid QuoteId,
        decimal RequestedLimit,
        decimal Retention,
        decimal LocalPremium,
        string LocalStatus);

    private sealed record ProviderMarketIndicationResponse(
        RatingProviderMarketDisposition MarketDisposition,
        string ProviderReference,
        string ProviderQuoteNumber,
        decimal? IndicatedPremium,
        decimal? IndicatedLimit,
        decimal? IndicatedRetention);
}
