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

namespace LIAnsureProtect.IntegrationTests;

public sealed class ClaimEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>,
      IDisposable
{
    private const string ClaimsEndpointPath = "/api/v1/claims";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");
    private static readonly DateTime EffectiveDateUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection submissionConnection;
    private readonly SqliteConnection claimsConnection;
    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient httpClient;

    public ClaimEndpointTests(WebApplicationFactory<Program> webApplicationFactory)
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

    [Theory]
    [InlineData("Customer")]
    [InlineData("Broker")]
    public async Task File_Claim_Returns_Created_For_Owned_Bound_Policy(string role)
    {
        var policy = await SeedBoundPolicyAsync("customer-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            ClaimsEndpointPath,
            role,
            "customer-1",
            CreateFileClaimRequest(policy.Id));
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;
        Assert.StartsWith("CLM-CYB-", root.GetProperty("claimNumber").GetString(), StringComparison.Ordinal);
        Assert.Equal("Filed", root.GetProperty("status").GetString());
        Assert.Equal(policy.Id, root.GetProperty("policyId").GetGuid());
        Assert.Equal(policy.PolicyNumber, root.GetProperty("policyNumber").GetString());
    }

    [Fact]
    public async Task File_Claim_Writes_ClaimFiled_Event_To_The_Module_Outbox()
    {
        var policy = await SeedBoundPolicyAsync("customer-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            ClaimsEndpointPath,
            "Customer",
            "customer-1",
            CreateFileClaimRequest(policy.Id));
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = webApplicationFactory.Services.CreateScope();
        var claimsDbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        var outboxMessage = Assert.Single(
            await claimsDbContext.OutboxMessages.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal("ClaimFiledDomainEvent", outboxMessage.Type);
        Assert.Null(outboxMessage.ProcessedAtUtc);
    }

    [Fact]
    public async Task File_Claim_Returns_NotFound_For_Unknown_Policy()
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            ClaimsEndpointPath,
            "Customer",
            "customer-1",
            CreateFileClaimRequest(Guid.NewGuid()));
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task File_Claim_Returns_NotFound_For_Someone_Elses_Policy()
    {
        var policy = await SeedBoundPolicyAsync("customer-2");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            ClaimsEndpointPath,
            "Customer",
            "customer-1",
            CreateFileClaimRequest(policy.Id));
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task File_Claim_Returns_Conflict_When_Incident_Is_Outside_The_Policy_Period()
    {
        var policy = await SeedBoundPolicyAsync("customer-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            ClaimsEndpointPath,
            "Customer",
            "customer-1",
            new
            {
                policyId = policy.Id,
                incidentType = "RansomwareExtortion",
                incidentAtUtc = EffectiveDateUtc.AddYears(-1),
                discoveredAtUtc = EffectiveDateUtc.AddDays(1),
                description = "Incident before the policy period."
            });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task File_Claim_Returns_BadRequest_For_Missing_Description()
    {
        var policy = await SeedBoundPolicyAsync("customer-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            ClaimsEndpointPath,
            "Customer",
            "customer-1",
            new
            {
                policyId = policy.Id,
                incidentType = "RansomwareExtortion",
                incidentAtUtc = EffectiveDateUtc.AddDays(30),
                discoveredAtUtc = EffectiveDateUtc.AddDays(31),
                description = "   "
            });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task File_Claim_Returns_BadRequest_For_Unknown_Incident_Type()
    {
        var policy = await SeedBoundPolicyAsync("customer-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            ClaimsEndpointPath,
            "Customer",
            "customer-1",
            new
            {
                policyId = policy.Id,
                incidentType = "MeteorStrike",
                incidentAtUtc = EffectiveDateUtc.AddDays(30),
                discoveredAtUtc = EffectiveDateUtc.AddDays(31),
                description = "Not a cyber incident category."
            });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task File_Claim_Returns_Forbidden_For_Underwriter()
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            ClaimsEndpointPath,
            "Underwriter",
            "underwriter-1",
            CreateFileClaimRequest(Guid.NewGuid()));
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task File_Claim_With_Idempotency_Key_Replays_The_Original_Response()
    {
        var policy = await SeedBoundPolicyAsync("customer-1");
        var idempotencyKey = Guid.NewGuid().ToString();
        var body = CreateFileClaimRequest(policy.Id);

        using var firstRequest = CreateAuthenticatedRequest(
            HttpMethod.Post, ClaimsEndpointPath, "Customer", "customer-1", body, idempotencyKey);
        using var firstResponse = await httpClient.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        var firstContent = await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var secondRequest = CreateAuthenticatedRequest(
            HttpMethod.Post, ClaimsEndpointPath, "Customer", "customer-1", body, idempotencyKey);
        using var secondResponse = await httpClient.SendAsync(secondRequest, TestContext.Current.CancellationToken);
        var secondContent = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        using var firstPayload = JsonDocument.Parse(firstContent);
        using var secondPayload = JsonDocument.Parse(secondContent);
        Assert.Equal(
            firstPayload.RootElement.GetProperty("claimId").GetGuid(),
            secondPayload.RootElement.GetProperty("claimId").GetGuid());

        using var scope = webApplicationFactory.Services.CreateScope();
        var claimsDbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        Assert.Equal(1, await claimsDbContext.Claims.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task List_Claims_Returns_Only_The_Callers_Claims()
    {
        var ownedPolicy = await SeedBoundPolicyAsync("customer-1");
        var otherPolicy = await SeedBoundPolicyAsync("customer-2");
        await FileClaimAsync("customer-1", ownedPolicy.Id);
        await FileClaimAsync("customer-2", otherPolicy.Id);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get, ClaimsEndpointPath, "Customer", "customer-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(content);
        var claims = payload.RootElement.GetProperty("claims");
        Assert.Equal(1, claims.GetArrayLength());
        Assert.Equal(ownedPolicy.Id, claims[0].GetProperty("policyId").GetGuid());
    }

    [Fact]
    public async Task Claim_Detail_Returns_Timeline_For_The_Owner()
    {
        var policy = await SeedBoundPolicyAsync("customer-1");
        var claimId = await FileClaimAsync("customer-1", policy.Id);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{ClaimsEndpointPath}/{claimId}", "Customer", "customer-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;
        Assert.Equal(claimId, root.GetProperty("claimId").GetGuid());
        Assert.Equal("Filed", root.GetProperty("status").GetString());
        Assert.Equal(1_000_000m, root.GetProperty("policyLimitAtFiling").GetDecimal());
        var timeline = root.GetProperty("timeline");
        Assert.Equal(1, timeline.GetArrayLength());
        Assert.Equal("ClaimFiled", timeline[0].GetProperty("entryType").GetString());
    }

    [Fact]
    public async Task Claim_Detail_Returns_NotFound_For_Someone_Elses_Claim()
    {
        var policy = await SeedBoundPolicyAsync("customer-2");
        var claimId = await FileClaimAsync("customer-2", policy.Id);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{ClaimsEndpointPath}/{claimId}", "Customer", "customer-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Claim_Endpoints_Require_Authentication()
    {
        using var response = await httpClient.GetAsync(
            new Uri(ClaimsEndpointPath, UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
        submissionConnection.Dispose();
        claimsConnection.Dispose();
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

    private async Task<Guid> FileClaimAsync(string ownerUserId, Guid policyId)
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            ClaimsEndpointPath,
            "Customer",
            ownerUserId,
            CreateFileClaimRequest(policyId));
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var payload = JsonDocument.Parse(content);

        return payload.RootElement.GetProperty("claimId").GetGuid();
    }

    private static object CreateFileClaimRequest(Guid policyId)
    {
        return new
        {
            policyId,
            incidentType = "RansomwareExtortion",
            incidentAtUtc = EffectiveDateUtc.AddDays(60),
            discoveredAtUtc = EffectiveDateUtc.AddDays(62),
            description = "Ransomware encrypted the file server; extortion note received."
        };
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
