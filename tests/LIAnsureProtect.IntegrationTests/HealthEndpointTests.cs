using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;
using LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace LIAnsureProtect.IntegrationTests;


public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private const string RootEndpointPath = "/";
    private const string HealthEndpointPath = "/api/v1/health";
    private const string LivenessEndpointPath = "/api/v1/health/live";
    private const string ReadinessEndpointPath = "/api/v1/health/ready";
    private const string RunningStatus = "Running";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");

    private readonly SqliteConnection submissionConnection = new("DataSource=:memory:");
    private readonly SqliteConnection notificationsConnection = new("DataSource=:memory:");
    private readonly SqliteConnection underwritingConnection = new("DataSource=:memory:");
    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient _httpClient;

    public HealthEndpointTests(WebApplicationFactory<Program> webApplicationFactory)
    {
        submissionConnection.Open();
        notificationsConnection.Open();
        underwritingConnection.Open();

        this.webApplicationFactory = webApplicationFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDbContextOptionsConfiguration<SubmissionDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<NotificationsDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<UnderwritingDbContext>>();
                    services.RemoveAll<DbContextOptions>();
                    services.RemoveAll<DbContextOptions<SubmissionDbContext>>();
                    services.RemoveAll<DbContextOptions<NotificationsDbContext>>();
                    services.RemoveAll<DbContextOptions<UnderwritingDbContext>>();

                    services.AddDbContext<SubmissionDbContext>(options => options.UseSqlite(submissionConnection));
                    services.AddDbContext<NotificationsDbContext>(options => options.UseSqlite(notificationsConnection));
                    services.AddDbContext<UnderwritingDbContext>(options => options.UseSqlite(underwritingConnection));
                });

                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                });
            });

        using (var scope = this.webApplicationFactory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SubmissionDbContext>().Database.EnsureCreated();
            scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.EnsureCreated();
            scope.ServiceProvider.GetRequiredService<UnderwritingDbContext>().Database.EnsureCreated();
        }

        _httpClient = this.webApplicationFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = TestServerBaseAddress,
            AllowAutoRedirect = false
        });
    }



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



    [Theory]
    [InlineData(HealthEndpointPath)]
    [InlineData(LivenessEndpointPath)]
    [InlineData(ReadinessEndpointPath)]
    public async Task Health_Routes_Return_Success(string requestUri)
    {
        // Act
        using var response = await _httpClient.GetAsync(requestUri, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", content);
    }



    [Fact]
    public async Task Request_With_Correlation_Header_Echoes_Correlation_Header()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, RootEndpointPath);
        request.Headers.Add(CorrelationIdHeaderName, "submission-flow-123");

        // Act
        using var response = await _httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.Headers.TryGetValues(CorrelationIdHeaderName, out var values));
        Assert.Equal("submission-flow-123", Assert.Single(values));
    }



    [Fact]
    public async Task Request_Without_Correlation_Header_Returns_Generated_Correlation_Header()
    {
        // Act
        using var response = await _httpClient.GetAsync(RootEndpointPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.Headers.TryGetValues(CorrelationIdHeaderName, out var values));
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(values)));
    }



    public void Dispose()
    {
        _httpClient.Dispose();
        webApplicationFactory.Dispose();
        submissionConnection.Dispose();
        notificationsConnection.Dispose();
        underwritingConnection.Dispose();
    }
}
