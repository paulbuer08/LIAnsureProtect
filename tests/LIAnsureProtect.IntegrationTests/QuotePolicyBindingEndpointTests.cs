using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LIAnsureProtect.Domain.Policies;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Persistence.Outbox;
using LIAnsureProtect.IntegrationTests.Security;
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

public sealed class QuotePolicyBindingEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>,
      IDisposable
{
    private const string QuotesEndpointPath = "/api/v1/quotes";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");

    private readonly SqliteConnection databaseConnection;
    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient httpClient;

    public QuotePolicyBindingEndpointTests(WebApplicationFactory<Program> webApplicationFactory)
    {
        databaseConnection = new SqliteConnection("DataSource=:memory:");
        databaseConnection.Open();

        this.webApplicationFactory = webApplicationFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<SubmissionDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<DbContextOptions<SubmissionDbContext>>();
                services.AddDbContext<SubmissionDbContext>(options =>
                {
                    options.UseSqlite(databaseConnection);
                });

                services
                    .AddAuthentication(TestAuthHandler.AuthenticationScheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.AuthenticationScheme,
                        options => { });
            });
        });

        using var scope = this.webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        dbContext.Database.EnsureCreated();

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
    [InlineData("Admin")]
    public async Task Accept_Quote_Returns_Ok_For_Owned_Eligible_Quote(string role)
    {
        var quote = CreateQuotedQuote("customer-1");
        await SaveQuoteAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/accept",
            role,
            "customer-1",
            CreateAcceptQuoteRequest());
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(quote.Id, root.GetProperty("quoteId").GetGuid());
        Assert.Equal("Accepted", root.GetProperty("status").GetString());
        Assert.Equal("Jane Applicant", root.GetProperty("acceptedByName").GetString());
        Assert.True(root.GetProperty("subjectivitiesAcknowledged").GetBoolean());

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedQuote = await dbContext.Quotes.SingleAsync(
            saved => saved.Id == quote.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(QuoteStatus.Accepted, savedQuote.Status);
        Assert.Equal("customer-1", savedQuote.AcceptedByUserId);

        var outboxMessage = await dbContext.Set<OutboxMessage>().SingleAsync(
            message => message.Type == nameof(QuoteAcceptedDomainEvent),
            TestContext.Current.CancellationToken);
        Assert.Contains(quote.Id.ToString(), outboxMessage.Payload);
        Assert.Contains("customer-1", outboxMessage.Payload);
    }

    [Fact]
    public async Task Accept_Quote_Returns_Forbidden_For_Underwriter()
    {
        var quote = CreateQuotedQuote("customer-1");
        await SaveQuoteAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/accept",
            "Underwriter",
            "underwriter-1",
            CreateAcceptQuoteRequest());
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Accept_Quote_Returns_Not_Found_For_Cross_Owner_Quote()
    {
        var quote = CreateQuotedQuote("customer-2");
        await SaveQuoteAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/accept",
            "Customer",
            "customer-1",
            CreateAcceptQuoteRequest());
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Accept_Quote_Returns_Conflict_For_Referred_Quote()
    {
        var quote = CreateReferredQuote("customer-1");
        await SaveQuoteAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/accept",
            "Customer",
            "customer-1",
            CreateAcceptQuoteRequest());
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Accept_Quote_Returns_Conflict_While_Control_Evidence_Is_Required()
    {
        var quote = CreateProvisionalQuotedQuote("customer-1");
        await SaveQuoteAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/accept",
            "Customer",
            "customer-1",
            CreateAcceptQuoteRequest());
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("control evidence", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bind_Accepted_Quote_Returns_Created_And_Persists_Policy_Attempt_And_Outbox()
    {
        var quote = CreateAcceptedQuote("customer-1");
        await SaveQuoteAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/bind",
            "Customer",
            "customer-1",
            CreateBindQuoteRequest());
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;
        var policyId = root.GetProperty("policyId").GetGuid();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/v1/policies/{policyId}", response.Headers.Location?.OriginalString);
        Assert.Equal("Bound", root.GetProperty("status").GetString());
        Assert.Equal(quote.Id, root.GetProperty("quoteId").GetGuid());
        Assert.Equal("LIP-CYB-", root.GetProperty("policyNumber").GetString()?[..8]);
        Assert.Equal("LIP-SIM-BIND-", root.GetProperty("bindingReference").GetString()?[..13]);

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedPolicy = await dbContext.Set<Policy>().SingleAsync(
            policy => policy.Id == policyId,
            TestContext.Current.CancellationToken);
        var bindingAttempt = await dbContext.Set<PolicyBindingAttempt>().SingleAsync(
            attempt => attempt.PolicyId == policyId,
            TestContext.Current.CancellationToken);
        var outboxMessage = await dbContext.Set<OutboxMessage>().SingleAsync(
            message => message.Type == nameof(PolicyBoundDomainEvent),
            TestContext.Current.CancellationToken);
        var savedQuote = await dbContext.Quotes.SingleAsync(
            saved => saved.Id == quote.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(PolicyStatus.Bound, savedPolicy.Status);
        Assert.Equal(QuoteStatus.Bound, savedQuote.Status);
        Assert.Equal(PolicyBindingAttemptStatus.Succeeded, bindingAttempt.Status);
        Assert.Contains(policyId.ToString(), outboxMessage.Payload);
    }

    [Fact]
    public async Task Bind_Quote_Returns_Forbidden_For_Underwriter()
    {
        var quote = CreateAcceptedQuote("customer-1");
        await SaveQuoteAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/bind",
            "Underwriter",
            "underwriter-1",
            CreateBindQuoteRequest());
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Bind_Quote_Returns_Not_Found_For_Cross_Owner_Quote()
    {
        var quote = CreateAcceptedQuote("customer-2");
        await SaveQuoteAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/bind",
            "Customer",
            "customer-1",
            CreateBindQuoteRequest());
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Accept_Quote_With_Idempotency_Key_Replays_Response_And_Does_Not_Reaccept()
    {
        var quote = CreateQuotedQuote("customer-1");
        var idempotencyKey = "accept-quote-key-1";
        await SaveQuoteAsync(quote);

        using var firstRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/accept",
            "Customer",
            "customer-1",
            CreateAcceptQuoteRequest(),
            idempotencyKey);
        using var firstResponse = await httpClient.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        var firstContent = await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var secondRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/accept",
            "Customer",
            "customer-1",
            CreateAcceptQuoteRequest(),
            idempotencyKey);
        using var secondResponse = await httpClient.SendAsync(secondRequest, TestContext.Current.CancellationToken);
        var secondContent = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstContent, secondContent);

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedQuote = await dbContext.Quotes.SingleAsync(
            saved => saved.Id == quote.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(QuoteStatus.Accepted, savedQuote.Status);
    }

    [Fact]
    public async Task Bind_Quote_With_Idempotency_Key_Replays_Response_And_Creates_One_Policy_Attempt_And_Outbox()
    {
        var quote = CreateAcceptedQuote("customer-1");
        var idempotencyKey = "bind-quote-key-1";
        await SaveQuoteAsync(quote);

        using var firstRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/bind",
            "Customer",
            "customer-1",
            CreateBindQuoteRequest(),
            idempotencyKey);
        using var firstResponse = await httpClient.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        var firstContent = await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var secondRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/bind",
            "Customer",
            "customer-1",
            CreateBindQuoteRequest(),
            idempotencyKey);
        using var secondResponse = await httpClient.SendAsync(secondRequest, TestContext.Current.CancellationToken);
        var secondContent = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(firstContent, secondContent);
        Assert.Equal(firstResponse.Headers.Location?.OriginalString, secondResponse.Headers.Location?.OriginalString);

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var policyCount = await dbContext.Set<Policy>().CountAsync(
            policy => policy.QuoteId == quote.Id,
            TestContext.Current.CancellationToken);
        var attemptCount = await dbContext.Set<PolicyBindingAttempt>().CountAsync(
            TestContext.Current.CancellationToken);
        var outboxCount = await dbContext.Set<OutboxMessage>().CountAsync(
            message => message.Type == nameof(PolicyBoundDomainEvent),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, policyCount);
        Assert.Equal(1, attemptCount);
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task Bind_Quote_Returns_Conflict_When_Idempotency_Key_Is_Reused_With_Different_Body()
    {
        var quote = CreateAcceptedQuote("customer-1");
        var idempotencyKey = "bind-quote-key-2";
        await SaveQuoteAsync(quote);

        using var firstRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/bind",
            "Customer",
            "customer-1",
            CreateBindQuoteRequest(),
            idempotencyKey);
        using var firstResponse = await httpClient.SendAsync(firstRequest, TestContext.Current.CancellationToken);

        using var secondRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/bind",
            "Customer",
            "customer-1",
            new
            {
                effectiveDateUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            idempotencyKey);
        using var secondResponse = await httpClient.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Broker")]
    [InlineData("Admin")]
    public async Task Policy_Reads_Return_Owned_Policy_With_Contractual_And_Coverage_State(string role)
    {
        var policyId = await BindPolicyAsync("customer-1");

        using var listRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/policies",
            role,
            "customer-1");
        using var listResponse = await httpClient.SendAsync(
            listRequest,
            TestContext.Current.CancellationToken);
        var listPayload = await listResponse.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        using var detailRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/policies/{policyId}",
            role,
            "customer-1");
        using var detailResponse = await httpClient.SendAsync(
            detailRequest,
            TestContext.Current.CancellationToken);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Single(listPayload.GetProperty("policies").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal(policyId, detail.GetProperty("policyId").GetGuid());
        Assert.Equal("Bound", detail.GetProperty("contractualStatus").GetString());
        var coverageState = detail.GetProperty("coverageState").GetString();
        Assert.True(
            coverageState is "Scheduled" or "Active" or "Expired",
            $"Unexpected coverage state '{coverageState}'.");
        Assert.Equal("Example Company", detail.GetProperty("companyName").GetString());
        Assert.Equal("Moderate", detail.GetProperty("quoteRiskTierAtBind").GetString());
    }

    [Fact]
    public async Task Policy_Detail_Returns_Not_Found_For_Another_Owner()
    {
        var policyId = await BindPolicyAsync("customer-2");

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/policies/{policyId}",
            "Customer",
            "customer-1");
        using var response = await httpClient.SendAsync(
            request,
            TestContext.Current.CancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 404 but received {(int)response.StatusCode}: {responseBody}");
    }

    [Fact]
    public async Task Policy_List_Searches_Only_The_Owners_Policies()
    {
        var ownedPolicyId = await BindPolicyAsync("customer-1");
        var otherPolicyId = await BindPolicyAsync("customer-2");

        using var ownedRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/policies?search={ownedPolicyId}",
            "Customer",
            "customer-1");
        using var ownedResponse = await httpClient.SendAsync(
            ownedRequest,
            TestContext.Current.CancellationToken);
        var ownedPayload = await ownedResponse.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        using var otherRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/policies?search={otherPolicyId}",
            "Customer",
            "customer-1");
        using var otherResponse = await httpClient.SendAsync(
            otherRequest,
            TestContext.Current.CancellationToken);
        var otherPayload = await otherResponse.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, ownedResponse.StatusCode);
        Assert.Equal(ownedPolicyId, Assert.Single(ownedPayload.GetProperty("policies").EnumerateArray()).GetProperty("policyId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, otherResponse.StatusCode);
        Assert.Empty(otherPayload.GetProperty("policies").EnumerateArray());
    }

    [Fact]
    public async Task Policy_List_Returns_Forbidden_For_Operational_Role()
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/policies",
            "Underwriter",
            "underwriter-1");
        using var response = await httpClient.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string path,
        string role,
        string userId,
        object? body = null,
        string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.EmailHeader, $"{userId}@example.com");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.Add("Idempotency-Key", idempotencyKey);

        return request;
    }

    private async Task SaveQuoteAsync(SeededQuote quote)
    {
        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        await dbContext.Submissions.AddAsync(quote.Submission, TestContext.Current.CancellationToken);
        await dbContext.Quotes.AddAsync(quote.Quote, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> BindPolicyAsync(string ownerUserId)
    {
        var quote = CreateAcceptedQuote(ownerUserId);
        await SaveQuoteAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QuotesEndpointPath}/{quote.Id}/bind",
            "Customer",
            ownerUserId,
            CreateBindQuoteRequest());
        using var response = await httpClient.SendAsync(
            request,
            TestContext.Current.CancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return payload.GetProperty("policyId").GetGuid();
    }

    private static SeededQuote CreateQuotedQuote(string ownerUserId)
    {
        var submission = CreateSubmittedSubmission(ownerUserId);
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
            new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc));

        return new SeededQuote(submission, quote);
    }

    private static SeededQuote CreateAcceptedQuote(string ownerUserId)
    {
        var quote = CreateQuotedQuote(ownerUserId);
        quote.Quote.Accept(
            ownerUserId,
            "Jane Applicant",
            "Chief Financial Officer",
            subjectivitiesAcknowledged: true,
            acceptedAtUtc: new DateTime(2026, 6, 21, 2, 0, 0, DateTimeKind.Utc));
        quote.Quote.ClearDomainEvents();

        return quote;
    }

    private static SeededQuote CreateProvisionalQuotedQuote(string ownerUserId)
    {
        var submission = CreateSubmittedSubmission(ownerUserId);
        var quote = Quote.Generate(
            submission.Id,
            ownerUserId,
            12_000m,
            1_000_000m,
            10_000m,
            CyberRiskTier.Moderate,
            "BaselineCyber",
            ["MFA evidence required."],
            [],
            new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc),
            evidenceRequiredCount: 1);

        return new SeededQuote(submission, quote);
    }

    private static SeededQuote CreateReferredQuote(string ownerUserId)
    {
        var submission = CreateSubmittedSubmission(ownerUserId);
        var quote = Quote.Generate(
            submission.Id,
            ownerUserId,
            premium: 18_000m,
            requestedLimit: 5_000_000m,
            retention: 10_000m,
            CyberRiskTier.Severe,
            "HighRiskCyber",
            ["MFA evidence required."],
            ["Severe risk tier requires underwriter review."],
            new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc));

        return new SeededQuote(submission, quote);
    }

    private static Submission CreateSubmittedSubmission(string ownerUserId)
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            ownerUserId,
            new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc));
        submission.Submit();
        submission.ClearDomainEvents();

        return submission;
    }

    private static object CreateAcceptQuoteRequest()
    {
        return new
        {
            acceptedByName = "Jane Applicant",
            acceptedByTitle = "Chief Financial Officer",
            subjectivitiesAcknowledged = true
        };
    }

    private static object CreateBindQuoteRequest()
    {
        return new
        {
            effectiveDateUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private sealed record SeededQuote(Submission Submission, Quote Quote)
    {
        public Guid Id => Quote.Id;
    }

    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
        databaseConnection.Dispose();
    }
}
