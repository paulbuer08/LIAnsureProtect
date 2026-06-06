using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace LIAnsureProtect.IntegrationTests;


public sealed class HealthEndpointTests(WebApplicationFactory<Program> webApplicationFactory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string RootEndpointPath = "/";
    private const string HealthEndpointPath = "/api/v1/health";
    private const string RunningStatus = "Running";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");

    private readonly HttpClient _httpClient = webApplicationFactory
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            });
        })
        .CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = TestServerBaseAddress,
            AllowAutoRedirect = false
        });



    [Fact]
    public async Task Root_Endpoint_Returns_Application_Status()
    {
        // Arrange
        var requestUri = RootEndpointPath;
        var expectedApplicationName = typeof(Program).Assembly.GetName().Name;

        // Act
        using var response = await _httpClient.GetAsync(requestUri, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedApplicationName, root.GetProperty("application").GetString());
        Assert.Equal(RunningStatus, root.GetProperty("status").GetString());
    }



    [Fact]
    public async Task Health_Endpoint_Returns_Success()
    {
        // Arrange
        var requestUri = HealthEndpointPath;

        // Act
        using var response = await _httpClient.GetAsync(requestUri, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", content);
    }
}
