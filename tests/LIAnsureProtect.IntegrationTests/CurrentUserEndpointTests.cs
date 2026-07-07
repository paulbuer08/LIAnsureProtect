using System.Net;
using System.Text.Json;
using LIAnsureProtect.IntegrationTests.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LIAnsureProtect.IntegrationTests;

/// <summary>
/// The provider-neutral "who am I" endpoint the SPA calls to learn its roles — the single source
/// of truth shared with the API's authorization policies (both read <c>ICurrentUser</c>). This
/// removes the SPA's dependence on parsing the ID token for roles.
/// </summary>
public sealed class CurrentUserEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>,
      IDisposable
{
    private const string MeEndpointPath = "/api/v1/me";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");

    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient httpClient;

    public CurrentUserEndpointTests(WebApplicationFactory<Program> webApplicationFactory)
    {
        this.webApplicationFactory = webApplicationFactory.WithWebHostBuilder(builder =>
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

        httpClient = this.webApplicationFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = TestServerBaseAddress,
                AllowAutoRedirect = false
            });
    }

    [Fact]
    public async Task Returns_Identity_And_Roles_For_An_Authenticated_Caller()
    {
        using var request = CreateAuthenticatedRequest("adjuster-1", "ClaimsAdjuster", "adjuster@example.com");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;
        Assert.Equal("adjuster-1", root.GetProperty("userId").GetString());
        Assert.Equal("adjuster@example.com", root.GetProperty("email").GetString());
        var roles = root.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ToArray();
        Assert.Contains("ClaimsAdjuster", roles);
    }

    [Fact]
    public async Task Returns_Empty_Roles_When_The_Caller_Has_No_Role_Assigned()
    {
        using var request = CreateAuthenticatedRequest("customer-1", role: null, "customer@example.com");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(content);
        Assert.Empty(payload.RootElement.GetProperty("roles").EnumerateArray());
    }

    [Fact]
    public async Task Returns_Unauthorized_For_An_Anonymous_Caller()
    {
        using var response = await httpClient.GetAsync(
            new Uri(MeEndpointPath, UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        string userId,
        string? role,
        string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(MeEndpointPath, UriKind.Relative));
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.EmailHeader, email);
        if (!string.IsNullOrWhiteSpace(role))
            request.Headers.Add(TestAuthHandler.RolesHeader, role);

        return request;
    }
}
