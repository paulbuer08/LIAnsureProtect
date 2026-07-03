using System.Net;
using System.Net.Http.Json;
using LIAnsureProtect.IntegrationTests.Security;
using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.GetEvidenceReferenceData;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LIAnsureProtect.IntegrationTests;

/// <summary>
/// Covers the evidence reference-data endpoint (the first production cache-aside adoption):
/// any authenticated role can read it, anonymous callers cannot, and the payload carries the
/// same categories and upload rules the workflows enforce.
/// </summary>
public sealed class EvidenceReferenceEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string ReferenceEndpointPath = "/api/v1/evidence-requests/reference";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");

    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient httpClient;

    public EvidenceReferenceEndpointTests(WebApplicationFactory<Program> factory)
    {
        webApplicationFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services
                    .AddAuthentication(TestAuthHandler.AuthenticationScheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.AuthenticationScheme,
                        options => { });
            });
        });

        httpClient = webApplicationFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = TestServerBaseAddress,
            AllowAutoRedirect = false
        });
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Broker")]
    [InlineData("Underwriter")]
    [InlineData("Admin")]
    public async Task Any_Authenticated_Role_Reads_Categories_And_Upload_Rules(string role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ReferenceEndpointPath);
        request.Headers.Add(TestAuthHandler.UserIdHeader, $"{role.ToLowerInvariant()}-reference-user");
        request.Headers.Add(TestAuthHandler.EmailHeader, "reference-user@example.com");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);

        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<EvidenceReferenceDataResult>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(payload);
        Assert.Contains("MultiFactorAuthentication", payload!.Categories);
        Assert.Contains(payload.AllowedContentTypes, allowed => allowed.ContentType == "application/pdf");
        Assert.Equal(5, payload.MaximumDocumentCount);
    }

    [Fact]
    public async Task Anonymous_Callers_Are_Rejected()
    {
        using var response = await httpClient.GetAsync(
            new Uri(ReferenceEndpointPath, UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
    }
}
