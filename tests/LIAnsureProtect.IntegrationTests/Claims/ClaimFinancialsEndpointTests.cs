using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LIAnsureProtect.Domain.Policies;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.IntegrationTests.Security;
using LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;
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

namespace LIAnsureProtect.IntegrationTests.Claims;

public sealed class ClaimFinancialsEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>,
      IDisposable
{
    private const string ClaimsEndpointPath = "/api/v1/claims";
    private const string AdjudicationEndpointPath = "/api/v1/claims/adjudication";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");
    private static readonly DateTime EffectiveDateUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection submissionConnection;
    private readonly SqliteConnection claimsConnection;
    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient httpClient;

    public ClaimFinancialsEndpointTests(WebApplicationFactory<Program> webApplicationFactory)
    {
        submissionConnection = new SqliteConnection("DataSource=:memory:");
        submissionConnection.Open();
        claimsConnection = new SqliteConnection("DataSource=:memory:");
        claimsConnection.Open();

        this.webApplicationFactory = webApplicationFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<SubmissionDbContext>>();
                services.RemoveAll<DbContextOptions<SubmissionDbContext>>();
                services.AddDbContext<SubmissionDbContext>(options => options.UseSqlite(submissionConnection));

                services.RemoveAll<IDbContextOptionsConfiguration<ClaimsDbContext>>();
                services.RemoveAll<DbContextOptions<ClaimsDbContext>>();
                services.AddDbContext<ClaimsDbContext>(options => options.UseSqlite(claimsConnection));

                services
                    .AddAuthentication(TestAuthHandler.AuthenticationScheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.AuthenticationScheme,
                        options => { });
            });
        });

        using var scope = this.webApplicationFactory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SubmissionDbContext>().Database.EnsureCreated();
        scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Database.EnsureCreated();

        httpClient = this.webApplicationFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = TestServerBaseAddress,
                AllowAutoRedirect = false
            });
    }

    [Fact]
    public async Task Owner_Declares_The_Claimed_Amount_And_Sees_It_On_The_Detail()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{ClaimsEndpointPath}/{claimId}/claimed-amount", "Customer", "customer-1",
            new { amount = 250_000m });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var payload = JsonDocument.Parse(content))
        {
            Assert.Equal(250_000m, payload.RootElement.GetProperty("claimedAmount").GetDecimal());
            Assert.Equal(0m, payload.RootElement.GetProperty("paidAmount").GetDecimal());
        }

        using var detailRequest = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{ClaimsEndpointPath}/{claimId}", "Customer", "customer-1");
        using var detailResponse = await httpClient.SendAsync(detailRequest, TestContext.Current.CancellationToken);
        var detailContent = await detailResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var detail = JsonDocument.Parse(detailContent);
        Assert.Equal(250_000m, detail.RootElement.GetProperty("claimedAmount").GetDecimal());
        // The reserve is the insurer's internal estimate — the claimant must never see it.
        Assert.DoesNotContain("reserveAmount", detailContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reserveHistory", detailContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Claimed_Amount_Is_Owner_Scoped()
    {
        var claimId = await SeedAndFileClaimAsync("customer-2");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{ClaimsEndpointPath}/{claimId}/claimed-amount", "Customer", "customer-1",
            new { amount = 250_000m });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Non_Positive_Claimed_Amount_Returns_BadRequest()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{ClaimsEndpointPath}/{claimId}/claimed-amount", "Customer", "customer-1",
            new { amount = 0m });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Assigned_Adjuster_Sets_And_Adjusts_The_Reserve_With_History()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");
        await AssignAsync(claimId, "adjuster-1");

        using var setRequest = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/reserve", "ClaimsAdjuster", "adjuster-1",
            new { amount = 150_000m, reason = "Initial estimate from forensic scoping call." });
        using var setResponse = await httpClient.SendAsync(setRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);

        using var adjustRequest = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/reserve", "ClaimsAdjuster", "adjuster-1",
            new { amount = 90_000m, reason = "Backups recovered; exposure reduced." });
        using var adjustResponse = await httpClient.SendAsync(adjustRequest, TestContext.Current.CancellationToken);
        var adjustContent = await adjustResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, adjustResponse.StatusCode);
        using (var payload = JsonDocument.Parse(adjustContent))
        {
            Assert.Equal(90_000m, payload.RootElement.GetProperty("reserveAmount").GetDecimal());
        }

        using var detailRequest = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{AdjudicationEndpointPath}/{claimId}", "ClaimsAdjuster", "adjuster-1");
        using var detailResponse = await httpClient.SendAsync(detailRequest, TestContext.Current.CancellationToken);
        var detailContent = await detailResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var detail = JsonDocument.Parse(detailContent);
        Assert.Equal(90_000m, detail.RootElement.GetProperty("reserveAmount").GetDecimal());
        var history = detail.RootElement.GetProperty("reserveHistory");
        Assert.Equal(2, history.GetArrayLength());
        Assert.Equal(0m, history[0].GetProperty("oldAmount").GetDecimal());
        Assert.Equal(150_000m, history[0].GetProperty("newAmount").GetDecimal());
        Assert.Equal(150_000m, history[1].GetProperty("oldAmount").GetDecimal());
        Assert.Equal(90_000m, history[1].GetProperty("newAmount").GetDecimal());
    }

    [Fact]
    public async Task Reserve_Requires_Assignment()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/reserve", "ClaimsAdjuster", "adjuster-1",
            new { amount = 150_000m, reason = "Initial estimate." });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Only_The_Assigned_Adjuster_Can_Move_The_Reserve()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");
        await AssignAsync(claimId, "adjuster-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/reserve", "ClaimsAdjuster", "adjuster-2",
            new { amount = 150_000m, reason = "Second opinion." });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reserve_Change_Requires_A_Reason()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");
        await AssignAsync(claimId, "adjuster-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/reserve", "ClaimsAdjuster", "adjuster-1",
            new { amount = 150_000m, reason = "  " });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reserve_Is_Forbidden_For_Customers()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/reserve", "Customer", "customer-1",
            new { amount = 150_000m, reason = "Nice try." });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
        submissionConnection.Dispose();
        claimsConnection.Dispose();
    }

    private async Task AssignAsync(Guid claimId, string adjusterUserId)
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/assign-to-me", "ClaimsAdjuster", adjusterUserId);
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Guid> SeedAndFileClaimAsync(string ownerUserId)
    {
        var policy = await SeedBoundPolicyAsync(ownerUserId);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            ClaimsEndpointPath,
            "Customer",
            ownerUserId,
            new
            {
                policyId = policy.Id,
                incidentType = "RansomwareExtortion",
                incidentAtUtc = EffectiveDateUtc.AddDays(60),
                discoveredAtUtc = EffectiveDateUtc.AddDays(62),
                description = "Ransomware encrypted the file server; extortion note received."
            });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var payload = JsonDocument.Parse(content);

        return payload.RootElement.GetProperty("claimId").GetGuid();
    }

    private async Task<Policy> SeedBoundPolicyAsync(string ownerUserId)
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            ownerUserId,
            new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc));
        submission.Submit();
        submission.ClearDomainEvents();

        var quote = Quote.Generate(
            submission.Id,
            ownerUserId,
            premium: 12_000m,
            requestedLimit: 1_000_000m,
            retention: 10_000m,
            CyberRiskTier.Moderate,
            "BaselineCyber",
            ["MFA is implemented."],
            [],
            new DateTime(2025, 12, 10, 0, 0, 0, DateTimeKind.Utc));
        quote.Accept(ownerUserId, "Jane Applicant", "Chief Financial Officer", true,
            new DateTime(2025, 12, 15, 0, 0, 0, DateTimeKind.Utc));
        quote.ClearDomainEvents();

        var policy = Policy.BindFromAcceptedQuote(
            quote,
            $"LIP-CYB-20260101-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            ownerUserId,
            EffectiveDateUtc,
            new DateTime(2025, 12, 20, 0, 0, 0, DateTimeKind.Utc));
        policy.ClearDomainEvents();
        quote.MarkBound(new DateTime(2025, 12, 20, 0, 0, 0, DateTimeKind.Utc));
        quote.ClearDomainEvents();

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
        await dbContext.Quotes.AddAsync(quote, TestContext.Current.CancellationToken);
        await dbContext.Policies.AddAsync(policy, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return policy;
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string requestPath,
        string role,
        string userId,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, new Uri(requestPath, UriKind.Relative));
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.RolesHeader, role);

        if (body is not null)
            request.Content = JsonContent.Create(body);

        return request;
    }
}
