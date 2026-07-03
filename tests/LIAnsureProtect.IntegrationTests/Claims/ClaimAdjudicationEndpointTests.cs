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

public sealed class ClaimAdjudicationEndpointTests
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

    public ClaimAdjudicationEndpointTests(WebApplicationFactory<Program> webApplicationFactory)
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
    [InlineData("ClaimsAdjuster")]
    [InlineData("Admin")]
    public async Task Queue_Lists_Open_Claims_For_Adjusters(string role)
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");

        using var request = CreateAuthenticatedRequest(HttpMethod.Get, AdjudicationEndpointPath, role, "adjuster-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(content);
        var claims = payload.RootElement.GetProperty("claims");
        Assert.Equal(1, claims.GetArrayLength());
        Assert.Equal(claimId, claims[0].GetProperty("claimId").GetGuid());
        Assert.Equal("Filed", claims[0].GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Broker")]
    [InlineData("Underwriter")]
    public async Task Queue_Is_Forbidden_For_Non_Adjusters(string role)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, AdjudicationEndpointPath, role, "user-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Assign_To_Me_Claims_The_File_And_Starts_Review()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/assign-to-me", "ClaimsAdjuster", "adjuster-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(content);
        Assert.Equal("adjuster-1", payload.RootElement.GetProperty("assignedAdjusterUserId").GetString());
        Assert.Equal("UnderReview", payload.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Second_Adjuster_Assignment_Returns_Conflict_And_First_Survives()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");
        await AssignAsync(claimId, "adjuster-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/assign-to-me", "ClaimsAdjuster", "adjuster-2");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var detail = await GetAdjudicationDetailAsync(claimId);
        Assert.Equal("adjuster-1", detail.RootElement.GetProperty("assignedAdjusterUserId").GetString());
        detail.Dispose();
    }

    [Fact]
    public async Task Release_Then_Reassign_Hands_The_File_Over()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");
        await AssignAsync(claimId, "adjuster-1");

        using var releaseRequest = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/release-assignment", "ClaimsAdjuster", "adjuster-1");
        using var releaseResponse = await httpClient.SendAsync(releaseRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);

        using var reassignRequest = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/assign-to-me", "ClaimsAdjuster", "adjuster-2");
        using var reassignResponse = await httpClient.SendAsync(reassignRequest, TestContext.Current.CancellationToken);
        var content = await reassignResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, reassignResponse.StatusCode);
        using var payload = JsonDocument.Parse(content);
        Assert.Equal("adjuster-2", payload.RootElement.GetProperty("assignedAdjusterUserId").GetString());
    }

    [Fact]
    public async Task Work_Notes_Are_Appended_And_Visible_In_The_Detail()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");
        await AssignAsync(claimId, "adjuster-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{AdjudicationEndpointPath}/{claimId}/notes",
            "ClaimsAdjuster",
            "adjuster-1",
            new { note = "Called the insured; forensic report expected Friday." });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var detail = await GetAdjudicationDetailAsync(claimId);
        var notes = detail.RootElement.GetProperty("workNotes");
        Assert.Equal(1, notes.GetArrayLength());
        Assert.Equal("adjuster-1", notes[0].GetProperty("createdByUserId").GetString());
        detail.Dispose();
    }

    [Fact]
    public async Task Information_Request_Round_Trip_Flows_Between_Adjuster_And_Claimant()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");
        await AssignAsync(claimId, "adjuster-1");

        // Adjuster asks.
        using var askRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{AdjudicationEndpointPath}/{claimId}/information-requests",
            "ClaimsAdjuster",
            "adjuster-1",
            new { title = "Proof of loss", message = "Please provide the forensic report and the extortion note." });
        using var askResponse = await httpClient.SendAsync(askRequest, TestContext.Current.CancellationToken);
        var askContent = await askResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, askResponse.StatusCode);
        Guid requestId;
        using (var askPayload = JsonDocument.Parse(askContent))
        {
            requestId = askPayload.RootElement.GetProperty("informationRequestId").GetGuid();
        }

        // The claimant sees the question on their own detail.
        using var ownerDetailRequest = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{ClaimsEndpointPath}/{claimId}", "Customer", "customer-1");
        using var ownerDetailResponse = await httpClient.SendAsync(ownerDetailRequest, TestContext.Current.CancellationToken);
        var ownerDetailContent = await ownerDetailResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using (var ownerDetail = JsonDocument.Parse(ownerDetailContent))
        {
            Assert.Equal("InformationRequested", ownerDetail.RootElement.GetProperty("status").GetString());
            var requests = ownerDetail.RootElement.GetProperty("informationRequests");
            Assert.Equal(1, requests.GetArrayLength());
            Assert.Equal("Proof of loss", requests[0].GetProperty("title").GetString());
        }

        // The claimant answers.
        using var respondRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{ClaimsEndpointPath}/{claimId}/information-requests/{requestId}/respond",
            "Customer",
            "customer-1",
            new { responseText = "Forensic report attached; ransom note photographed." });
        using var respondResponse = await httpClient.SendAsync(respondRequest, TestContext.Current.CancellationToken);
        var respondContent = await respondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, respondResponse.StatusCode);
        using (var respondPayload = JsonDocument.Parse(respondContent))
        {
            Assert.True(respondPayload.RootElement.GetProperty("isAnswered").GetBoolean());
        }

        // The claim is back under review, and the adjuster sees the answer + full timeline.
        var detail = await GetAdjudicationDetailAsync(claimId);
        Assert.Equal("UnderReview", detail.RootElement.GetProperty("status").GetString());
        var answered = detail.RootElement.GetProperty("informationRequests");
        Assert.Equal("Forensic report attached; ransom note photographed.",
            answered[0].GetProperty("responseText").GetString());
        var timelineTypes = detail.RootElement.GetProperty("timeline")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("entryType").GetString())
            .ToArray();
        Assert.Contains("InformationRequested", timelineTypes);
        Assert.Contains("ClaimantResponded", timelineTypes);
        detail.Dispose();
    }

    [Fact]
    public async Task Claimant_Cannot_Answer_Someone_Elses_Information_Request()
    {
        var claimId = await SeedAndFileClaimAsync("customer-2");
        await AssignAsync(claimId, "adjuster-1");
        using var askRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{AdjudicationEndpointPath}/{claimId}/information-requests",
            "ClaimsAdjuster",
            "adjuster-1",
            new { title = "Proof of loss", message = "Please provide the forensic report." });
        using var askResponse = await httpClient.SendAsync(askRequest, TestContext.Current.CancellationToken);
        var askContent = await askResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Guid requestId;
        using (var askPayload = JsonDocument.Parse(askContent))
        {
            requestId = askPayload.RootElement.GetProperty("informationRequestId").GetGuid();
        }

        using var respondRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{ClaimsEndpointPath}/{claimId}/information-requests/{requestId}/respond",
            "Customer",
            "customer-1",
            new { responseText = "I should not be able to answer this." });
        using var respondResponse = await httpClient.SendAsync(respondRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respondResponse.StatusCode);
    }

    [Fact]
    public async Task Adjudication_Actions_Are_Forbidden_For_Customers()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"{AdjudicationEndpointPath}/{claimId}/assign-to-me", "Customer", "customer-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Assignment_Writes_ClaimAssigned_Event_To_The_Module_Outbox()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");
        await AssignAsync(claimId, "adjuster-1");

        using var scope = webApplicationFactory.Services.CreateScope();
        var claimsDbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        var types = await claimsDbContext.OutboxMessages
            .AsNoTracking()
            .Select(message => message.Type)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains("ClaimAssignedDomainEvent", types);
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

    private async Task<JsonDocument> GetAdjudicationDetailAsync(Guid claimId)
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{AdjudicationEndpointPath}/{claimId}", "ClaimsAdjuster", "adjuster-observer");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(content);
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
