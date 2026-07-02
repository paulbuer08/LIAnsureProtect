using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LIAnsureProtect.IntegrationTests;

/// <summary>
/// Covers the M44 API hardening: security response headers on every response, and HTTP 429 when a
/// caller exceeds the (test-tightened) unsafe-method rate limit. Uses anonymous requests so it needs
/// no database or auth harness — the global limiter runs before authorization.
/// </summary>
public sealed class SecurityAndRateLimitingEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private static readonly Uri TestServerBaseAddress = new("https://localhost");

    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient httpClient;

    public SecurityAndRateLimitingEndpointTests(WebApplicationFactory<Program> factory)
    {
        webApplicationFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                // Tighten the unsafe-method limit so a small burst deterministically trips 429.
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:UnsafePermitLimit"] = "2",
                    ["RateLimiting:SafePermitLimit"] = "5000",
                    ["RateLimiting:WindowSeconds"] = "60"
                });
            });
        });

        httpClient = webApplicationFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = TestServerBaseAddress,
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Responses_Carry_Security_Headers()
    {
        var response = await httpClient.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.True(response.Headers.Contains("Permissions-Policy"));
    }

    [Fact]
    public async Task Unsafe_Requests_Are_Rate_Limited_With_429()
    {
        var statuses = new List<HttpStatusCode>();

        // Unsafe limit is 2 → the 3rd+ POST in the window is rejected with 429 (before the ones
        // under the limit are rejected with 401 for being anonymous).
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/submissions");
            using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
    }
}
