using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace LIAnsureProtect.IntegrationTests.Claims;

public sealed class ClaimDocumentEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>,
      IDisposable
{
    private const string ClaimsEndpointPath = "/api/v1/claims";
    private const string AdjudicationEndpointPath = "/api/v1/claims/adjudication";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");
    private static readonly DateTime EffectiveDateUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection submissionConnection;
    private readonly SqliteConnection claimsConnection;
    private readonly string storageRootPath;
    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient httpClient;

    public ClaimDocumentEndpointTests(WebApplicationFactory<Program> webApplicationFactory)
    {
        submissionConnection = new SqliteConnection("DataSource=:memory:");
        submissionConnection.Open();
        claimsConnection = new SqliteConnection("DataSource=:memory:");
        claimsConnection.Open();
        storageRootPath = Path.Combine(Path.GetTempPath(), "liansureprotect-claim-documents", Guid.NewGuid().ToString("N"));

        this.webApplicationFactory = webApplicationFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DocumentStorage:LocalRootPath"] = storageRootPath
                });
            });

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
    public async Task Upload_Scans_Clean_And_Round_Trips_For_Owner_And_Adjuster()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");

        using var uploadRequest = CreateDocumentUpload(
            claimId, "Customer", "customer-1", "ForensicReport",
            [("forensic-report.pdf", "application/pdf", "clean pdf bytes")]);
        using var uploadResponse = await httpClient.SendAsync(uploadRequest, TestContext.Current.CancellationToken);
        var uploadContent = await uploadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        Guid documentId;
        using (var payload = JsonDocument.Parse(uploadContent))
        {
            var document = Assert.Single(payload.RootElement.GetProperty("documents").EnumerateArray());
            Assert.Equal("Clean", document.GetProperty("scanStatus").GetString());
            Assert.True(document.GetProperty("isDownloadAvailable").GetBoolean());
            Assert.Equal("ForensicReport", document.GetProperty("kind").GetString());
            documentId = document.GetProperty("documentId").GetGuid();
            // The private storage key must never appear in any payload.
            Assert.DoesNotContain("storageKey", uploadContent, StringComparison.OrdinalIgnoreCase);
        }

        // Owner download.
        using var ownerDownload = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{ClaimsEndpointPath}/{claimId}/documents/{documentId}/download", "Customer", "customer-1");
        using var ownerDownloadResponse = await httpClient.SendAsync(ownerDownload, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ownerDownloadResponse.StatusCode);
        Assert.Equal("clean pdf bytes",
            await ownerDownloadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Adjuster download.
        using var adjusterDownload = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{AdjudicationEndpointPath}/{claimId}/documents/{documentId}/download", "ClaimsAdjuster", "adjuster-1");
        using var adjusterDownloadResponse = await httpClient.SendAsync(adjusterDownload, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, adjusterDownloadResponse.StatusCode);
        Assert.Equal("clean pdf bytes",
            await adjusterDownloadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rejected_Scan_Locks_The_Download_And_Allows_A_Replacement()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");

        using var uploadRequest = CreateDocumentUpload(
            claimId, "Customer", "customer-1", "ProofOfLoss",
            [("infected.pdf", "application/pdf", "MALWARE-TEST-SIGNAL payload")]);
        using var uploadResponse = await httpClient.SendAsync(uploadRequest, TestContext.Current.CancellationToken);
        var uploadContent = await uploadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        Guid rejectedDocumentId;
        using (var payload = JsonDocument.Parse(uploadContent))
        {
            var document = Assert.Single(payload.RootElement.GetProperty("documents").EnumerateArray());
            Assert.Equal("Rejected", document.GetProperty("scanStatus").GetString());
            Assert.False(document.GetProperty("isDownloadAvailable").GetBoolean());
            rejectedDocumentId = document.GetProperty("documentId").GetGuid();
        }

        // Fail-closed: nobody can download it — not the owner, not the adjuster.
        using var ownerDownload = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{ClaimsEndpointPath}/{claimId}/documents/{rejectedDocumentId}/download", "Customer", "customer-1");
        using var ownerDownloadResponse = await httpClient.SendAsync(ownerDownload, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, ownerDownloadResponse.StatusCode);

        using var adjusterDownload = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{AdjudicationEndpointPath}/{claimId}/documents/{rejectedDocumentId}/download", "ClaimsAdjuster", "adjuster-1");
        using var adjusterDownloadResponse = await httpClient.SendAsync(adjusterDownload, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, adjusterDownloadResponse.StatusCode);

        // Replacement appends; the rejected original stays visible for audit.
        using var replacementRequest = CreateDocumentUpload(
            claimId, "Customer", "customer-1", "ProofOfLoss",
            [("proof-of-loss-clean.pdf", "application/pdf", "clean replacement bytes")]);
        using var replacementResponse = await httpClient.SendAsync(replacementRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, replacementResponse.StatusCode);

        using var detailRequest = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{AdjudicationEndpointPath}/{claimId}", "ClaimsAdjuster", "adjuster-1");
        using var detailResponse = await httpClient.SendAsync(detailRequest, TestContext.Current.CancellationToken);
        var detailContent = await detailResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var detail = JsonDocument.Parse(detailContent);
        var documents = detail.RootElement.GetProperty("documents");
        Assert.Equal(2, documents.GetArrayLength());
        var statuses = documents.EnumerateArray()
            .Select(document => document.GetProperty("scanStatus").GetString())
            .ToArray();
        Assert.Contains("Rejected", statuses);
        Assert.Contains("Clean", statuses);
        Assert.DoesNotContain("storageKey", detailContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Failed_Scan_Locks_The_Download()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");

        using var uploadRequest = CreateDocumentUpload(
            claimId, "Customer", "customer-1", "Other",
            [("flaky.pdf", "application/pdf", "SCAN-FAIL-TEST-SIGNAL payload")]);
        using var uploadResponse = await httpClient.SendAsync(uploadRequest, TestContext.Current.CancellationToken);
        var uploadContent = await uploadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(uploadContent);
        var document = Assert.Single(payload.RootElement.GetProperty("documents").EnumerateArray());
        Assert.Equal("Failed", document.GetProperty("scanStatus").GetString());
        Assert.False(document.GetProperty("isDownloadAvailable").GetBoolean());
    }

    [Fact]
    public async Task Upload_To_Someone_Elses_Claim_Returns_NotFound()
    {
        var claimId = await SeedAndFileClaimAsync("customer-2");

        using var uploadRequest = CreateDocumentUpload(
            claimId, "Customer", "customer-1", "Invoice",
            [("invoice.pdf", "application/pdf", "bytes")]);
        using var uploadResponse = await httpClient.SendAsync(uploadRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task Upload_Rejects_Unsupported_Content_Types()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");

        using var uploadRequest = CreateDocumentUpload(
            claimId, "Customer", "customer-1", "Other",
            [("malware.exe", "application/x-msdownload", "MZ bytes")]);
        using var uploadResponse = await httpClient.SendAsync(uploadRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task Upload_Rejects_An_Unknown_Document_Kind()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");

        using var uploadRequest = CreateDocumentUpload(
            claimId, "Customer", "customer-1", "SurpriseKind",
            [("invoice.pdf", "application/pdf", "bytes")]);
        using var uploadResponse = await httpClient.SendAsync(uploadRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task Upload_Is_Forbidden_For_Underwriters()
    {
        using var uploadRequest = CreateDocumentUpload(
            Guid.NewGuid(), "Underwriter", "underwriter-1", "Invoice",
            [("invoice.pdf", "application/pdf", "bytes")]);
        using var uploadResponse = await httpClient.SendAsync(uploadRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task Owner_Detail_Lists_Documents_With_Scan_State()
    {
        var claimId = await SeedAndFileClaimAsync("customer-1");
        using var uploadRequest = CreateDocumentUpload(
            claimId, "Customer", "customer-1", "Invoice",
            [("invoice.pdf", "application/pdf", "clean bytes")]);
        using var uploadResponse = await httpClient.SendAsync(uploadRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        using var detailRequest = CreateAuthenticatedRequest(
            HttpMethod.Get, $"{ClaimsEndpointPath}/{claimId}", "Customer", "customer-1");
        using var detailResponse = await httpClient.SendAsync(detailRequest, TestContext.Current.CancellationToken);
        var detailContent = await detailResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        using var detail = JsonDocument.Parse(detailContent);
        var document = Assert.Single(detail.RootElement.GetProperty("documents").EnumerateArray());
        Assert.Equal("invoice.pdf", document.GetProperty("originalFileName").GetString());
        Assert.Equal("Clean", document.GetProperty("scanStatus").GetString());
    }

    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
        submissionConnection.Dispose();
        claimsConnection.Dispose();
        if (Directory.Exists(storageRootPath))
            Directory.Delete(storageRootPath, recursive: true);
    }

    private static HttpRequestMessage CreateDocumentUpload(
        Guid claimId,
        string role,
        string userId,
        string kind,
        IReadOnlyCollection<(string FileName, string ContentType, string Content)> files)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(kind), "kind" }
        };

        foreach (var file in files)
        {
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(file.Content));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "attachments", file.FileName);
        }

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{ClaimsEndpointPath}/{claimId}/documents",
            role,
            userId);
        request.Content = content;

        return request;
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
