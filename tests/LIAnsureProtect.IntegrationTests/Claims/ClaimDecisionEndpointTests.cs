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

public sealed class ClaimDecisionEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>,
      IDisposable
{
    private const string ClaimsEndpointPath = "/api/v1/claims";
    private const string AdjudicationEndpointPath = "/api/v1/claims/adjudication";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");
    private static readonly DateTime EffectiveDateUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    // Seeded policies carry limit 1,000,000 and retention 10,000 → cap 990,000.
    private const decimal SettlementCap = 990_000m;

    private readonly SqliteConnection submissionConnection;
    private readonly SqliteConnection claimsConnection;
    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient httpClient;

    public ClaimDecisionEndpointTests(WebApplicationFactory<Program> webApplicationFactory)
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
    public async Task Accept_Settles_Pays_And_Shows_The_Verdict_To_Both_Sides()
    {
        var claimId = await SeedFileAndAssignAsync("customer-1", "adjuster-1");

        using var acceptRequest = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/accept", "ClaimsAdjuster", "adjuster-1",
            new { settlementAmount = 300_000m, reason = "Covered ransomware loss; forensic report verified.", notes = "Wire authorized." });
        using var acceptResponse = await httpClient.SendAsync(acceptRequest, TestContext.Current.CancellationToken);
        var acceptContent = await acceptResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        using (var payload = JsonDocument.Parse(acceptContent))
        {
            Assert.Equal("Accepted", payload.RootElement.GetProperty("outcome").GetString());
            Assert.Equal(300_000m, payload.RootElement.GetProperty("settlementAmount").GetDecimal());
            Assert.Equal(300_000m, payload.RootElement.GetProperty("paidAmount").GetDecimal());
        }

        // The claimant sees the verdict and the money.
        using var ownerDetail = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{ClaimsEndpointPath}/{claimId}", "Customer", "customer-1");
        using var ownerDetailResponse = await httpClient.SendAsync(ownerDetail, TestContext.Current.CancellationToken);
        var ownerContent = await ownerDetailResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using (var owner = JsonDocument.Parse(ownerContent))
        {
            Assert.Equal("Accepted", owner.RootElement.GetProperty("status").GetString());
            Assert.Equal(300_000m, owner.RootElement.GetProperty("settlementAmount").GetDecimal());
            Assert.Equal(300_000m, owner.RootElement.GetProperty("paidAmount").GetDecimal());
        }

        // The adjudication detail shows the append-only audit.
        using var detailRequest = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{AdjudicationEndpointPath}/{claimId}", "ClaimsAdjuster", "adjuster-1");
        using var detailResponse = await httpClient.SendAsync(detailRequest, TestContext.Current.CancellationToken);
        var detailContent = await detailResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var detail = JsonDocument.Parse(detailContent);
        var decision = Assert.Single(detail.RootElement.GetProperty("decisions").EnumerateArray());
        Assert.Equal("Accepted", decision.GetProperty("outcome").GetString());
        Assert.Equal("adjuster-1", decision.GetProperty("decidedByUserId").GetString());
    }

    [Fact]
    public async Task Settlement_Over_The_Cap_Is_Rejected()
    {
        var claimId = await SeedFileAndAssignAsync("customer-1", "adjuster-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/accept", "ClaimsAdjuster", "adjuster-1",
            new { settlementAmount = SettlementCap + 0.01m, reason = "Too generous." });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Settlement_At_Exactly_The_Cap_Is_Accepted()
    {
        var claimId = await SeedFileAndAssignAsync("customer-1", "adjuster-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/accept", "ClaimsAdjuster", "adjuster-1",
            new { settlementAmount = SettlementCap, reason = "Total loss; full limit less retention." });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task No_Decision_Without_Assignment()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/accept", "ClaimsAdjuster", "adjuster-1",
            new { settlementAmount = 1_000m, reason = "Unassigned decision." });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Another_Adjuster_Cannot_Decide()
    {
        var claimId = await SeedFileAndAssignAsync("customer-1", "adjuster-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/deny", "ClaimsAdjuster", "adjuster-2",
            new { reasonCategory = "NotCovered", narrative = "Not my file but denying anyway." });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Denial_Requires_A_Narrative()
    {
        var claimId = await SeedFileAndAssignAsync("customer-1", "adjuster-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/deny", "ClaimsAdjuster", "adjuster-1",
            new { reasonCategory = "NotCovered", narrative = "  " });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Denial_With_An_Unknown_Category_Is_Rejected()
    {
        var claimId = await SeedFileAndAssignAsync("customer-1", "adjuster-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/deny", "ClaimsAdjuster", "adjuster-1",
            new { reasonCategory = "BadVibes", narrative = "Vibes were off." });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deny_Then_Close_Completes_The_Lifecycle_With_Events()
    {
        var claimId = await SeedFileAndAssignAsync("customer-1", "adjuster-1");

        using var denyRequest = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/deny", "ClaimsAdjuster", "adjuster-1",
            new { reasonCategory = "PolicyExclusion", narrative = "War exclusion applies." });
        using var denyResponse = await httpClient.SendAsync(denyRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, denyResponse.StatusCode);

        using var closeRequest = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/close", "ClaimsAdjuster", "adjuster-1");
        using var closeResponse = await httpClient.SendAsync(closeRequest, TestContext.Current.CancellationToken);
        var closeContent = await closeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        using (var payload = JsonDocument.Parse(closeContent))
        {
            Assert.Equal("Closed", payload.RootElement.GetProperty("status").GetString());
        }

        // The owner sees denial reason + narrative.
        using var ownerDetail = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{ClaimsEndpointPath}/{claimId}", "Customer", "customer-1");
        using var ownerDetailResponse = await httpClient.SendAsync(ownerDetail, TestContext.Current.CancellationToken);
        var ownerContent = await ownerDetailResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using (var owner = JsonDocument.Parse(ownerContent))
        {
            Assert.Equal("Closed", owner.RootElement.GetProperty("status").GetString());
            Assert.Equal("PolicyExclusion", owner.RootElement.GetProperty("denialReason").GetString());
            Assert.Equal("War exclusion applies.", owner.RootElement.GetProperty("denialNarrative").GetString());
        }

        // Both outcomes reached the module outbox.
        using var scope = webApplicationFactory.Services.CreateScope();
        var claimsDbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        var types = await claimsDbContext.OutboxMessages
            .AsNoTracking()
            .Select(message => message.Type)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains("ClaimDeniedDomainEvent", types);
        Assert.Contains("ClaimClosedDomainEvent", types);
    }

    [Fact]
    public async Task Close_Requires_A_Prior_Decision()
    {
        var claimId = await SeedFileAndAssignAsync("customer-1", "adjuster-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/close", "ClaimsAdjuster", "adjuster-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Accept_With_Idempotency_Key_Replays_And_Writes_One_Decision()
    {
        var claimId = await SeedFileAndAssignAsync("customer-1", "adjuster-1");
        var idempotencyKey = Guid.NewGuid().ToString();
        var body = new { settlementAmount = 300_000m, reason = "Covered loss." };

        using var firstRequest = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/accept", "ClaimsAdjuster", "adjuster-1",
            body, idempotencyKey);
        using var firstResponse = await httpClient.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        var firstContent = await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var secondRequest = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/accept", "ClaimsAdjuster", "adjuster-1",
            body, idempotencyKey);
        using var secondResponse = await httpClient.SendAsync(secondRequest, TestContext.Current.CancellationToken);
        var secondContent = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstContent, secondContent);

        using var detailRequest = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{AdjudicationEndpointPath}/{claimId}", "ClaimsAdjuster", "adjuster-1");
        using var detailResponse = await httpClient.SendAsync(detailRequest, TestContext.Current.CancellationToken);
        var detailContent = await detailResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var detail = JsonDocument.Parse(detailContent);
        Assert.Equal(1, detail.RootElement.GetProperty("decisions").GetArrayLength());
    }

    [Fact]
    public async Task Decisions_Are_Forbidden_For_Customers()
    {
        var claimId = await SeedFileAndAssignAsync("customer-1", "adjuster-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/accept", "Customer", "customer-1",
            new { settlementAmount = 1_000m, reason = "Paying myself." });
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

    private async Task<Guid> SeedFileAndAssignAsync(string ownerUserId, string adjusterUserId)
    {
        var claimId = await SeedAndFileClaimAsync(ownerUserId);
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/assign-to-me", "ClaimsAdjuster", adjusterUserId);
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return claimId;
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
        object? body = null,
        string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, new Uri(requestPath, UriKind.Relative));
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.RolesHeader, role);

        if (body is not null)
            request.Content = JsonContent.Create(body);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.Add("Idempotency-Key", idempotencyKey);

        return request;
    }
}
