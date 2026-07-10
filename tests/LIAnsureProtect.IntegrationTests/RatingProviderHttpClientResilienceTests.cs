using System.Net;
using System.Net.Http.Json;
using LIAnsureProtect.Application.Quotes.RatingProviders;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace LIAnsureProtect.IntegrationTests;

public sealed class RatingProviderHttpClientResilienceTests
{
    [Fact]
    public async Task Provider_Client_Retries_Transient_Server_Failure()
    {
        var handler = new SequenceHttpMessageHandler(
        [
            HttpStatusCode.InternalServerError,
            HttpStatusCode.BadGateway,
            HttpStatusCode.OK
        ]);
        var providerClient = CreateProviderClient(handler);

        var result = await providerClient.GetMarketIndicationAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(RatingProviderAttemptStatus.Succeeded, result.Status);
        Assert.Equal(3, handler.CallCount);
        Assert.Equal(3, result.AttemptCount);
    }

    [Fact]
    public async Task Provider_Client_Returns_Circuit_Open_After_Repeated_Server_Failures()
    {
        var handler = new SequenceHttpMessageHandler(
        [
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError
        ]);
        var providerClient = CreateProviderClient(handler);

        var results = new List<RatingProviderResult>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            results.Add(await providerClient.GetMarketIndicationAsync(
                CreateRequest(),
                TestContext.Current.CancellationToken));

            if (results[^1].Status == RatingProviderAttemptStatus.CircuitOpen)
                break;
        }

        Assert.Contains(results, result => result.Status == RatingProviderAttemptStatus.CircuitOpen);
        Assert.Contains(results, result => result.FailureCategory == RatingProviderFailureCategory.CircuitOpen);
    }

    private static IRatingProviderClient CreateProviderClient(SequenceHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(
            new PrimaryHandlerOverrideFilter(handler));
        services.AddInfrastructure("Host=localhost;Database=liansureprotect_test;Username=postgres;Password=postgres");

        var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<IRatingProviderClient>();
    }

    private static RatingProviderRequest CreateRequest()
    {
        return new RatingProviderRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "auth0|owner-user-1",
            CyberIndustryClass.ProfessionalServices,
            AnnualRevenueBand.From10MTo50M,
            1_000_000m,
            10_000m,
            CyberSecurityControlStatus.Implemented,
            CyberSecurityControlStatus.Implemented,
            BackupMaturity.Mature,
            true,
            0,
            SensitiveDataExposure.Moderate,
            null,
            null,
            null,
            10_000m,
            CyberRiskTier.Moderate,
            QuoteStatus.Quoted,
            "BaselineCyber");
    }

    private sealed class PrimaryHandlerOverrideFilter(HttpMessageHandler handler)
        : IHttpMessageHandlerBuilderFilter
    {
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
        {
            return builder =>
            {
                next(builder);
                builder.PrimaryHandler = handler;
            };
        }
    }

    private sealed class SequenceHttpMessageHandler(IReadOnlyList<HttpStatusCode> statuses)
        : HttpMessageHandler
    {
        private int callCount;

        public int CallCount => callCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var currentCall = Interlocked.Increment(ref callCount);
            var status = statuses[Math.Min(currentCall - 1, statuses.Count - 1)];

            if (status != HttpStatusCode.OK)
                return Task.FromResult(new HttpResponseMessage(status));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    marketDisposition = "Quoted",
                    providerReference = "CNT-REF-RESILIENCE",
                    providerQuoteNumber = "CNT-Q-RESILIENCE",
                    indicatedPremium = 10_500m,
                    indicatedLimit = 1_000_000m,
                    indicatedRetention = 10_000m
                })
            });
        }
    }
}
