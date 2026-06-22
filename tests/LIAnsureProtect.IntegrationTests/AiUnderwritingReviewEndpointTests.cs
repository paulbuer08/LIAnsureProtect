using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LIAnsureProtect.Application.Quotes.Ai;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Infrastructure.Persistence;
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

public sealed class AiUnderwritingReviewEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>,
      IDisposable
{
    private const string QueueEndpointPath = "/api/v1/underwriting/quote-referrals";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");

    private readonly SqliteConnection databaseConnection;
    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient httpClient;

    public AiUnderwritingReviewEndpointTests(WebApplicationFactory<Program> webApplicationFactory)
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

    [Fact]
    public async Task Generate_Ai_Review_Returns_Forbidden_For_Customer()
    {
        var quote = CreateReferredQuote("customer-1");
        await SaveQuoteAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/ai-review",
            "Customer",
            "customer-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Generate_Ai_Review_Persists_Advisory_Output_And_Does_Not_Mutate_Quote()
    {
        var quote = CreateReferredQuote("customer-1");
        await SaveQuoteAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/ai-review",
            "Underwriter",
            "underwriter-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(quote.Id, root.GetProperty("quoteId").GetGuid());
        Assert.Equal("Succeeded", root.GetProperty("status").GetString());
        Assert.Equal(AiReviewConstants.AdvisoryDisclaimer, root.GetProperty("advisoryDisclaimer").GetString());
        Assert.Contains(
            root.GetProperty("suggestedUnderwritingQuestions").EnumerateArray().Select(value => value.GetString()),
            value => value?.Contains("MFA", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains("quote.riskTier", root.GetProperty("citations").EnumerateArray().Select(value => value.GetString()));

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedQuote = await dbContext.Quotes.SingleAsync(
            saved => saved.Id == quote.Id,
            TestContext.Current.CancellationToken);
        var aiReview = await dbContext.Set<AiUnderwritingReview>().SingleAsync(
            review => review.QuoteId == quote.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(QuoteStatus.Referred, savedQuote.Status);
        Assert.Equal(18_000m, savedQuote.Premium);
        Assert.Equal(10_000m, savedQuote.Retention);
        Assert.Equal("MFA evidence required.", savedQuote.Subjectivities);
        Assert.Null(savedQuote.ReviewedByUserId);
        Assert.Equal(AiUnderwritingReviewStatus.Succeeded, aiReview.Status);
        Assert.Equal("underwriter-1", aiReview.RequestedByUserId);
        Assert.Equal(AiReviewConstants.PromptVersion, aiReview.PromptVersion);
        Assert.False(string.IsNullOrWhiteSpace(aiReview.InputSnapshotHash));
        Assert.Contains("quote.referralReasons", aiReview.Citations);
    }

    [Fact]
    public async Task Ai_Review_Failure_Does_Not_Block_Manual_Underwriting()
    {
        var quote = CreateReferredQuote("customer-1");
        await SaveQuoteAsync(quote);

        using var failureFactory = webApplicationFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAiReviewService>();
                services.AddScoped<IAiReviewService, FailingAiReviewService>();
            });
        });
        using var failureClient = failureFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = TestServerBaseAddress,
                AllowAutoRedirect = false
            });

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/ai-review",
            "Underwriter",
            "underwriter-1");
        using var response = await failureClient.SendAsync(request, TestContext.Current.CancellationToken);

        using var approveRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/approve",
            "Underwriter",
            "underwriter-1",
            new
            {
                reason = "Manual review remains authoritative.",
                notes = "AI failure did not block the underwriting queue."
            });
        using var approveResponse = await httpClient.SendAsync(approveRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedQuote = await dbContext.Quotes.SingleAsync(
            saved => saved.Id == quote.Id,
            TestContext.Current.CancellationToken);
        var aiReview = await dbContext.Set<AiUnderwritingReview>().SingleAsync(
            review => review.QuoteId == quote.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(AiUnderwritingReviewStatus.Failed, aiReview.Status);
        Assert.Equal("Simulated AI review failure requested by test service.", aiReview.FailureReason);
        Assert.Equal(QuoteStatus.Approved, savedQuote.Status);
        Assert.Equal("Manual review remains authoritative.", savedQuote.UnderwritingDecisionReason);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string path,
        string role,
        string userId,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.EmailHeader, $"{userId}@example.com");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);

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
            new DateTime(2026, 6, 22, 1, 0, 0, DateTimeKind.Utc));

        return new SeededQuote(submission, quote);
    }

    private static Submission CreateSubmittedSubmission(string ownerUserId)
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            ownerUserId,
            new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc));
        submission.Submit();
        submission.ClearDomainEvents();

        return submission;
    }

    private sealed record SeededQuote(Submission Submission, Quote Quote)
    {
        public Guid Id => Quote.Id;
    }

    private sealed class FailingAiReviewService : IAiReviewService
    {
        public Task<AiReviewProviderResult> GenerateUnderwritingReviewAsync(
            AiReviewProviderRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(AiReviewProviderResult.Failed(
                "Local Simulated AI",
                "Simulated AI review failure requested by test service.",
                new DateTime(2026, 6, 22, 1, 5, 0, DateTimeKind.Utc)));
        }
    }

    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
        databaseConnection.Dispose();
    }
}
