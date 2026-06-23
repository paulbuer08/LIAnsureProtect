using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace LIAnsureProtect.IntegrationTests;

public sealed class EvidenceDocumentEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>,
      IDisposable
{
    private static readonly Uri TestServerBaseAddress = new("https://localhost");

    private readonly SqliteConnection databaseConnection;
    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient httpClient;
    private readonly string storageRootPath;

    public EvidenceDocumentEndpointTests(WebApplicationFactory<Program> webApplicationFactory)
    {
        databaseConnection = new SqliteConnection("DataSource=:memory:");
        databaseConnection.Open();
        storageRootPath = Path.Combine(Path.GetTempPath(), "liansureprotect-evidence-documents", Guid.NewGuid().ToString("N"));

        this.webApplicationFactory = webApplicationFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DocumentStorage:LocalRootPath"] = storageRootPath
                });
            });

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
    public async Task Owner_Response_Can_Upload_Five_Evidence_Documents_And_Stores_File_Bytes_Outside_Database()
    {
        var (quote, evidenceRequest) = await SeedOpenEvidenceRequestAsync("customer-1");
        using var responseRequest = CreateAuthenticatedMultipartResponse(
            evidenceRequest.Id,
            "Customer",
            "customer-1",
            CreateEvidenceFiles(5));

        using var response = await httpClient.SendAsync(responseRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var documents = payload.RootElement.GetProperty("documents").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, documents.Length);
        Assert.All(documents, document => Assert.False(document.TryGetProperty("storageKey", out _)));
        Assert.All(documents, document =>
        {
            Assert.Equal("Clean", document.GetProperty("scanStatus").GetString());
            Assert.Equal("LocalDeterministicEvidenceDocumentScanner", document.GetProperty("scannerProviderName").GetString());
            Assert.Equal("NO_THREATS_FOUND", document.GetProperty("scanResultCode").GetString());
            Assert.True(document.GetProperty("isDownloadAvailable").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(document.GetProperty("sha256").GetString()));
        });

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedDocuments = await dbContext.Set<QuoteEvidenceDocument>()
            .Where(document => document.EvidenceRequestId == evidenceRequest.Id)
            .OrderBy(document => document.OriginalFileName)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(5, savedDocuments.Count);
        Assert.All(savedDocuments, document =>
        {
            Assert.Equal(quote.Id, document.QuoteId);
            Assert.Equal(quote.Quote.SubmissionId, document.SubmissionId);
            Assert.Equal("customer-1", document.OwnerUserId);
            Assert.Equal("customer-1", document.UploadedByUserId);
            Assert.False(string.IsNullOrWhiteSpace(document.StorageKey));
            Assert.True(File.Exists(Path.Combine(storageRootPath, document.StorageKey)));
            Assert.Equal(EvidenceDocumentScanStatus.Clean, document.ScanStatus);
            Assert.Equal("LocalDeterministicEvidenceDocumentScanner", document.ScannerProviderName);
            Assert.Equal("NO_THREATS_FOUND", document.ScanResultCode);
            Assert.Equal("No local test threat markers were found.", document.ScanResultReason);
            Assert.NotNull(document.ScannedAtUtc);
            Assert.False(string.IsNullOrWhiteSpace(document.Sha256));
            Assert.True(document.IsDownloadAvailable);
        });

        var firstStoredFile = Path.Combine(storageRootPath, savedDocuments[0].StorageKey);
        Assert.Equal("Evidence document 1", await File.ReadAllTextAsync(firstStoredFile, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Owner_Response_Records_Rejected_And_Failed_Scan_Metadata()
    {
        var (_, evidenceRequest) = await SeedOpenEvidenceRequestAsync("customer-1");
        using var responseRequest = CreateAuthenticatedMultipartResponse(
            evidenceRequest.Id,
            "Customer",
            "customer-1",
            [
                new EvidenceFile(
                    "rejected-evidence.txt",
                    "text/plain",
                    "This local test file contains MALWARE-TEST-SIGNAL."),
                new EvidenceFile(
                    "scan-failure-evidence.txt",
                    "text/plain",
                    "This local test file contains SCAN-FAIL-TEST-SIGNAL.")
            ]);

        using var response = await httpClient.SendAsync(responseRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var documents = payload.RootElement.GetProperty("documents").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(documents, document =>
            document.GetProperty("originalFileName").GetString() == "rejected-evidence.txt"
            && document.GetProperty("scanStatus").GetString() == "Rejected"
            && document.GetProperty("scanResultCode").GetString() == "THREATS_FOUND"
            && !document.GetProperty("isDownloadAvailable").GetBoolean());
        Assert.Contains(documents, document =>
            document.GetProperty("originalFileName").GetString() == "scan-failure-evidence.txt"
            && document.GetProperty("scanStatus").GetString() == "Failed"
            && document.GetProperty("scanResultCode").GetString() == "SCAN_FAILED"
            && !document.GetProperty("isDownloadAvailable").GetBoolean());

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedDocuments = await dbContext.Set<QuoteEvidenceDocument>()
            .Where(document => document.EvidenceRequestId == evidenceRequest.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains(savedDocuments, document =>
            document.OriginalFileName == "rejected-evidence.txt"
            && document.ScanStatus == EvidenceDocumentScanStatus.Rejected
            && document.ScanResultCode == "THREATS_FOUND"
            && !document.IsDownloadAvailable);
        Assert.Contains(savedDocuments, document =>
            document.OriginalFileName == "scan-failure-evidence.txt"
            && document.ScanStatus == EvidenceDocumentScanStatus.Failed
            && document.ScanResultCode == "SCAN_FAILED"
            && !document.IsDownloadAvailable);
    }

    [Fact]
    public async Task Evidence_Document_Download_Is_Private_To_Owner_And_Underwriters()
    {
        var (quote, evidenceRequest) = await SeedOpenEvidenceRequestAsync("customer-1");
        using var responseRequest = CreateAuthenticatedMultipartResponse(
            evidenceRequest.Id,
            "Customer",
            "customer-1",
            CreateEvidenceFiles(1));
        using var uploadResponse = await httpClient.SendAsync(responseRequest, TestContext.Current.CancellationToken);
        var uploadContent = await uploadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var uploadPayload = JsonDocument.Parse(uploadContent);
        var documentId = uploadPayload.RootElement.GetProperty("documents")[0].GetProperty("documentId").GetGuid();

        using var ownerDownloadRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/evidence-requests/{evidenceRequest.Id}/documents/{documentId}/download",
            "Customer",
            "customer-1");
        using var ownerDownloadResponse = await httpClient.SendAsync(ownerDownloadRequest, TestContext.Current.CancellationToken);

        using var differentOwnerDownloadRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/evidence-requests/{evidenceRequest.Id}/documents/{documentId}/download",
            "Customer",
            "customer-2");
        using var differentOwnerDownloadResponse = await httpClient.SendAsync(differentOwnerDownloadRequest, TestContext.Current.CancellationToken);

        using var underwriterDownloadRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/underwriting/quote-referrals/{quote.Id}/evidence-requests/{evidenceRequest.Id}/documents/{documentId}/download",
            "Underwriter",
            "underwriter-1");
        using var underwriterDownloadResponse = await httpClient.SendAsync(underwriterDownloadRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerDownloadResponse.StatusCode);
        Assert.Equal("Evidence document 1", await ownerDownloadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("text/plain", ownerDownloadResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "evidence-document-1.txt",
            ownerDownloadResponse.Content.Headers.ContentDisposition?.FileNameStar
                ?? ownerDownloadResponse.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal(HttpStatusCode.NotFound, differentOwnerDownloadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, underwriterDownloadResponse.StatusCode);
        Assert.Equal("Evidence document 1", await underwriterDownloadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Evidence_Document_Download_Is_Fail_Closed_When_Scan_Does_Not_Clean_Document()
    {
        var (quote, evidenceRequest) = await SeedOpenEvidenceRequestAsync("customer-1");
        using var responseRequest = CreateAuthenticatedMultipartResponse(
            evidenceRequest.Id,
            "Customer",
            "customer-1",
            [
                new EvidenceFile(
                    "rejected-evidence.txt",
                    "text/plain",
                    "This local test file contains MALWARE-TEST-SIGNAL.")
            ]);
        using var uploadResponse = await httpClient.SendAsync(responseRequest, TestContext.Current.CancellationToken);
        var uploadContent = await uploadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var uploadPayload = JsonDocument.Parse(uploadContent);
        var documentId = uploadPayload.RootElement.GetProperty("documents")[0].GetProperty("documentId").GetGuid();

        using var ownerDownloadRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/evidence-requests/{evidenceRequest.Id}/documents/{documentId}/download",
            "Customer",
            "customer-1");
        using var ownerDownloadResponse = await httpClient.SendAsync(ownerDownloadRequest, TestContext.Current.CancellationToken);
        var ownerDownloadContent = await ownerDownloadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var underwriterDownloadRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/underwriting/quote-referrals/{quote.Id}/evidence-requests/{evidenceRequest.Id}/documents/{documentId}/download",
            "Underwriter",
            "underwriter-1");
        using var underwriterDownloadResponse = await httpClient.SendAsync(underwriterDownloadRequest, TestContext.Current.CancellationToken);
        var underwriterDownloadContent = await underwriterDownloadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, ownerDownloadResponse.StatusCode);
        Assert.Contains("not trusted for download", ownerDownloadContent);
        Assert.DoesNotContain("MALWARE-TEST-SIGNAL", ownerDownloadContent);
        Assert.Equal(HttpStatusCode.Conflict, underwriterDownloadResponse.StatusCode);
        Assert.Contains("not trusted for download", underwriterDownloadContent);
        Assert.DoesNotContain("MALWARE-TEST-SIGNAL", underwriterDownloadContent);
    }

    [Fact]
    public async Task Underwriter_Cannot_Accept_Evidence_When_Documents_Are_Not_All_Clean()
    {
        var (quote, evidenceRequest) = await SeedOpenEvidenceRequestAsync("customer-1");
        using var responseRequest = CreateAuthenticatedMultipartResponse(
            evidenceRequest.Id,
            "Customer",
            "customer-1",
            [
                new EvidenceFile(
                    "rejected-evidence.txt",
                    "text/plain",
                    "This local test file contains MALWARE-TEST-SIGNAL.")
            ]);
        using var uploadResponse = await httpClient.SendAsync(responseRequest, TestContext.Current.CancellationToken);

        using var acceptRequest = CreateAuthenticatedJsonRequest(
            HttpMethod.Post,
            $"/api/v1/underwriting/quote-referrals/{quote.Id}/evidence-requests/{evidenceRequest.Id}/accept",
            "Underwriter",
            "underwriter-1",
            """{"reviewNotes":"MFA evidence is sufficient."}""");
        using var acceptResponse = await httpClient.SendAsync(acceptRequest, TestContext.Current.CancellationToken);
        var acceptContent = await acceptResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, acceptResponse.StatusCode);
        Assert.Contains("Only clean evidence documents can be accepted.", acceptContent);
    }

    [Fact]
    public async Task Underwriter_Cannot_Record_Evidence_Review_Decision_When_Documents_Are_Not_All_Clean()
    {
        var (quote, evidenceRequest) = await SeedOpenEvidenceRequestAsync("customer-1");
        using var responseRequest = CreateAuthenticatedMultipartResponse(
            evidenceRequest.Id,
            "Customer",
            "customer-1",
            [
                new EvidenceFile(
                    "rejected-evidence.txt",
                    "text/plain",
                    "This local test file contains MALWARE-TEST-SIGNAL.")
            ]);
        using var uploadResponse = await httpClient.SendAsync(responseRequest, TestContext.Current.CancellationToken);

        using var reviewRequest = CreateAuthenticatedJsonRequest(
            HttpMethod.Post,
            $"/api/v1/underwriting/quote-referrals/{quote.Id}/evidence-requests/{evidenceRequest.Id}/review-decision",
            "Underwriter",
            "underwriter-1",
            """
            {
              "decision": "NeedsClarification",
              "reason": "The uploaded evidence cannot be reviewed while the document scan is rejected.",
              "remediationGuidance": "Please upload a clean replacement evidence document."
            }
            """);
        using var reviewResponse = await httpClient.SendAsync(reviewRequest, TestContext.Current.CancellationToken);
        var reviewContent = await reviewResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reviewResponse.StatusCode);
        Assert.Contains("Only clean evidence documents can support a review decision.", reviewContent);
    }

    [Fact]
    public async Task Owner_Can_Upload_Replacement_Documents_When_Previous_Scan_Rejected_Or_Failed()
    {
        var (_, evidenceRequest) = await SeedOpenEvidenceRequestAsync("customer-1");
        using var rejectedResponseRequest = CreateAuthenticatedMultipartResponse(
            evidenceRequest.Id,
            "Customer",
            "customer-1",
            [
                new EvidenceFile(
                    "rejected-evidence.txt",
                    "text/plain",
                    "This local test file contains MALWARE-TEST-SIGNAL.")
            ]);
        using var rejectedResponse = await httpClient.SendAsync(rejectedResponseRequest, TestContext.Current.CancellationToken);

        using var replacementRequest = CreateAuthenticatedReplacementUpload(
            evidenceRequest.Id,
            "Customer",
            "customer-1",
            [
                new EvidenceFile(
                    "replacement-evidence.txt",
                    "text/plain",
                    "Replacement clean evidence document.")
            ]);
        using var replacementResponse = await httpClient.SendAsync(replacementRequest, TestContext.Current.CancellationToken);
        var replacementContent = await replacementResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(replacementContent);
        var documents = payload.RootElement.GetProperty("documents").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, rejectedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replacementResponse.StatusCode);
        Assert.Contains(documents, document =>
            document.GetProperty("originalFileName").GetString() == "rejected-evidence.txt"
            && document.GetProperty("scanStatus").GetString() == "Rejected");
        Assert.Contains(documents, document =>
            document.GetProperty("originalFileName").GetString() == "replacement-evidence.txt"
            && document.GetProperty("scanStatus").GetString() == "Clean");

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        Assert.Equal(
            2,
            await dbContext.Set<QuoteEvidenceDocument>()
                .CountAsync(document => document.EvidenceRequestId == evidenceRequest.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Owner_Replacement_Upload_Rejects_Empty_File_Set()
    {
        var (_, evidenceRequest) = await SeedOpenEvidenceRequestAsync("customer-1");
        using var rejectedResponseRequest = CreateAuthenticatedMultipartResponse(
            evidenceRequest.Id,
            "Customer",
            "customer-1",
            [
                new EvidenceFile(
                    "rejected-evidence.txt",
                    "text/plain",
                    "This local test file contains MALWARE-TEST-SIGNAL.")
            ]);
        using var rejectedResponse = await httpClient.SendAsync(rejectedResponseRequest, TestContext.Current.CancellationToken);

        using var emptyReplacementRequest = CreateAuthenticatedReplacementUpload(
            evidenceRequest.Id,
            "Customer",
            "customer-1",
            []);
        using var emptyReplacementResponse = await httpClient.SendAsync(emptyReplacementRequest, TestContext.Current.CancellationToken);
        var emptyReplacementContent = await emptyReplacementResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, rejectedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyReplacementResponse.StatusCode);
        Assert.Contains("at least one file", emptyReplacementContent);
    }

    [Fact]
    public async Task Owner_Response_Rejects_More_Than_Five_Evidence_Documents()
    {
        var (_, evidenceRequest) = await SeedOpenEvidenceRequestAsync("customer-1");
        using var responseRequest = CreateAuthenticatedMultipartResponse(
            evidenceRequest.Id,
            "Customer",
            "customer-1",
            CreateEvidenceFiles(6));

        using var response = await httpClient.SendAsync(responseRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("up to 5 files", content);
    }

    private HttpRequestMessage CreateAuthenticatedMultipartResponse(
        Guid evidenceRequestId,
        string role,
        string userId,
        IReadOnlyCollection<EvidenceFile> files)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent("Jane Applicant"), "respondentName" },
            { new StringContent("CISO"), "respondentTitle" },
            { new StringContent("MFA is enforced for all email and privileged accounts."), "responseText" }
        };

        foreach (var file in files)
        {
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(file.Content));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "attachments", file.FileName);
        }

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/evidence-requests/{evidenceRequestId}/respond",
            role,
            userId);
        request.Content = content;

        return request;
    }

    private HttpRequestMessage CreateAuthenticatedReplacementUpload(
        Guid evidenceRequestId,
        string role,
        string userId,
        IReadOnlyCollection<EvidenceFile> files)
    {
        var content = new MultipartFormDataContent();
        if (files.Count == 0)
            content.Add(new StringContent("true"), "emptyReplacementProbe");

        foreach (var file in files)
        {
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(file.Content));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "attachments", file.FileName);
        }

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/evidence-requests/{evidenceRequestId}/documents",
            role,
            userId);
        request.Content = content;

        return request;
    }

    private static HttpRequestMessage CreateAuthenticatedJsonRequest(
        HttpMethod method,
        string path,
        string role,
        string userId,
        string json)
    {
        var request = CreateAuthenticatedRequest(method, path, role, userId);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return request;
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

    private async Task<(SeededQuote Quote, QuoteEvidenceRequest EvidenceRequest)> SeedOpenEvidenceRequestAsync(
        string ownerUserId)
    {
        var quote = CreateReferredQuote(ownerUserId);
        var operation = QuoteReferralOperation.CreateDefault(
            quote.Id,
            quote.Quote.RiskTier,
            quote.Quote.CreatedAtUtc,
            quote.Quote.ExpiresAtUtc);
        var evidenceRequest = QuoteEvidenceRequest.Create(
            quote.Id,
            quote.Quote.SubmissionId,
            operation.Id,
            ownerUserId,
            "underwriter-1",
            EvidenceRequestCategory.MultiFactorAuthentication,
            "Confirm MFA rollout",
            "Please provide current MFA rollout evidence for privileged and email access.",
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc));

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        await dbContext.Submissions.AddAsync(quote.Submission, TestContext.Current.CancellationToken);
        await dbContext.Quotes.AddAsync(quote.Quote, TestContext.Current.CancellationToken);
        await dbContext.Set<QuoteReferralOperation>().AddAsync(operation, TestContext.Current.CancellationToken);
        await dbContext.Set<QuoteEvidenceRequest>().AddAsync(evidenceRequest, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (quote, evidenceRequest);
    }

    private static IReadOnlyCollection<EvidenceFile> CreateEvidenceFiles(int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new EvidenceFile(
                $"evidence-document-{index}.txt",
                "text/plain",
                $"Evidence document {index}"))
            .ToList();
    }

    private static SeededQuote CreateReferredQuote(string ownerUserId)
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            ownerUserId,
            new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc));
        submission.Submit();
        submission.ClearDomainEvents();

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

    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
        databaseConnection.Dispose();

        if (Directory.Exists(storageRootPath))
            Directory.Delete(storageRootPath, recursive: true);
    }

    private sealed record EvidenceFile(
        string FileName,
        string ContentType,
        string Content);

    private sealed record SeededQuote(Submission Submission, Quote Quote)
    {
        public Guid Id => Quote.Id;
    }
}
