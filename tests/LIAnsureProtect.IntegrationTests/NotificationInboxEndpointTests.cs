using System.Net;
using System.Text.Json;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.IntegrationTests.Security;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Domain;
using LIAnsureProtect.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace LIAnsureProtect.IntegrationTests;

public sealed class NotificationInboxEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>,
      IDisposable
{
    private const string EndpointPath = "/api/v1/notifications";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");

    private readonly SqliteConnection submissionConnection;
    private readonly SqliteConnection notificationsConnection;
    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient httpClient;

    public NotificationInboxEndpointTests(WebApplicationFactory<Program> webApplicationFactory)
    {
        submissionConnection = new SqliteConnection("DataSource=:memory:");
        submissionConnection.Open();
        notificationsConnection = new SqliteConnection("DataSource=:memory:");
        notificationsConnection.Open();

        this.webApplicationFactory = webApplicationFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<SubmissionDbContext>>();
                services.RemoveAll<DbContextOptions<SubmissionDbContext>>();
                services.AddDbContext<SubmissionDbContext>(options => options.UseSqlite(submissionConnection));

                // The inbox now lives in the Notifications module's own context/schema.
                services.RemoveAll<IDbContextOptionsConfiguration<NotificationsDbContext>>();
                services.RemoveAll<DbContextOptions<NotificationsDbContext>>();
                services.AddDbContext<NotificationsDbContext>(options => options.UseSqlite(notificationsConnection));

                services
                    .AddAuthentication(TestAuthHandler.AuthenticationScheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.AuthenticationScheme,
                        options => { });
            });
        });

        using var scope = this.webApplicationFactory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SubmissionDbContext>().Database.EnsureCreated();
        scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.EnsureCreated();

        httpClient = this.webApplicationFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = TestServerBaseAddress,
                AllowAutoRedirect = false
            });
    }

    [Fact]
    public async Task List_Returns_Only_Owner_Notifications_With_Unread_Count()
    {
        await SeedEntriesAsync(
            CreateEntry("customer-1", NotificationMessageTypes.QuoteReady),
            CreateEntry("customer-1", NotificationMessageTypes.EvidenceRequestRemediationRequired),
            CreateEntry("customer-2", NotificationMessageTypes.PolicyBound));

        using var request = CreateAuthenticatedRequest(HttpMethod.Get, EndpointPath, "Customer", "customer-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;
        var notifications = root.GetProperty("notifications").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, notifications.Length);
        Assert.Equal(2, root.GetProperty("unreadCount").GetInt32());
        Assert.Contains("Action needed on your evidence", content);
        Assert.DoesNotContain("Your policy is bound", content);
    }

    [Fact]
    public async Task Mark_Read_Flips_State_And_Reduces_Unread_Count()
    {
        var entry = CreateEntry("customer-1", NotificationMessageTypes.QuoteReady);
        await SeedEntriesAsync(entry);

        using var markRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{EndpointPath}/{entry.Id}/read",
            "Customer",
            "customer-1");
        using var markResponse = await httpClient.SendAsync(markRequest, TestContext.Current.CancellationToken);

        using var listRequest = CreateAuthenticatedRequest(HttpMethod.Get, EndpointPath, "Customer", "customer-1");
        using var listResponse = await httpClient.SendAsync(listRequest, TestContext.Current.CancellationToken);
        var listContent = await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var payload = JsonDocument.Parse(listContent);

        Assert.Equal(HttpStatusCode.OK, markResponse.StatusCode);
        Assert.Equal(0, payload.RootElement.GetProperty("unreadCount").GetInt32());
        Assert.True(payload.RootElement.GetProperty("notifications")[0].GetProperty("isRead").GetBoolean());
    }

    [Fact]
    public async Task Mark_Read_Returns_NotFound_For_Another_Owners_Notification()
    {
        var entry = CreateEntry("customer-1", NotificationMessageTypes.QuoteReady);
        await SeedEntriesAsync(entry);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{EndpointPath}/{entry.Id}/read",
            "Customer",
            "customer-2");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task SeedEntriesAsync(params NotificationInboxEntry[] entries)
    {
        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await dbContext.NotificationInboxEntries.AddRangeAsync(entries, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static NotificationInboxEntry CreateEntry(string recipientUserId, string type)
    {
        return NotificationInboxEntry.Create(
            recipientUserId,
            NotificationAudiences.CustomerOrBroker,
            type,
            "quote",
            Guid.NewGuid().ToString(),
            JsonSerializer.Serialize(new Dictionary<string, string> { ["actionRequired"] = "true" }),
            Guid.NewGuid(),
            new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 9, 0, 5, DateTimeKind.Utc));
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string path,
        string role,
        string userId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.EmailHeader, $"{userId}@example.com");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);
        return request;
    }

    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
        submissionConnection.Dispose();
        notificationsConnection.Dispose();
    }
}
