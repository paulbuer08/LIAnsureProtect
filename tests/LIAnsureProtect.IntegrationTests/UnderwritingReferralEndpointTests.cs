using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

public sealed class UnderwritingReferralEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>,
      IDisposable
{
    private const string QueueEndpointPath = "/api/v1/underwriting/quote-referrals";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");

    private readonly SqliteConnection databaseConnection;
    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient httpClient;

    public UnderwritingReferralEndpointTests(WebApplicationFactory<Program> webApplicationFactory)
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
    public async Task List_Quote_Referrals_Returns_Referred_Quotes_Oldest_First_For_Underwriter()
    {
        var olderQuote = CreateReferredQuote(
            "customer-1",
            new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc));
        var newerQuote = CreateReferredQuote(
            "customer-2",
            new DateTime(2026, 6, 21, 1, 0, 0, DateTimeKind.Utc));
        var quotedQuote = CreateQuotedQuote(
            "customer-3",
            new DateTime(2026, 6, 21, 2, 0, 0, DateTimeKind.Utc));

        await SaveQuotesAsync(olderQuote, newerQuote, quotedQuote);

        using var request = CreateAuthenticatedRequest(HttpMethod.Get, QueueEndpointPath, "Underwriter", "underwriter-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var referrals = payload.RootElement.GetProperty("quoteReferrals").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, referrals.Length);
        Assert.Equal(olderQuote.Id, referrals[0].GetProperty("quoteId").GetGuid());
        Assert.Equal(newerQuote.Id, referrals[1].GetProperty("quoteId").GetGuid());
    }

    [Fact]
    public async Task List_Quote_Referrals_Returns_Operations_Summary_For_Underwriter()
    {
        var quote = CreateReferredQuote(
            "customer-1",
            new DateTime(2026, 6, 22, 8, 0, 0, DateTimeKind.Utc));
        var operation = QuoteReferralOperation.CreateDefault(
            quote.Id,
            CyberRiskTier.Severe,
            quote.Quote.CreatedAtUtc,
            quote.Quote.ExpiresAtUtc);
        operation.AssignTo("underwriter-1", new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc));
        operation.AddTask(
            "underwriter-1",
            "Verify MFA evidence.",
            new DateTime(2026, 6, 23, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 9, 5, 0, DateTimeKind.Utc));
        await SaveQuotesAsync(quote);
        await SaveOperationsAsync(operation);

        using var request = CreateAuthenticatedRequest(HttpMethod.Get, QueueEndpointPath, "Underwriter", "underwriter-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var summary = payload.RootElement
            .GetProperty("quoteReferrals")[0]
            .GetProperty("operations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("underwriter-1", summary.GetProperty("assignedUnderwriterUserId").GetString());
        Assert.Equal("High", summary.GetProperty("priority").GetString());
        Assert.Equal("InReview", summary.GetProperty("status").GetString());
        Assert.Equal(1, summary.GetProperty("openTaskCount").GetInt32());
        Assert.False(summary.GetProperty("isSlaBreached").GetBoolean());
    }

    [Fact]
    public async Task Operations_Actions_Update_Assignment_Triage_Notes_Tasks_And_Timeline()
    {
        var quote = CreateReferredQuote("customer-1");
        var operation = QuoteReferralOperation.CreateDefault(
            quote.Id,
            quote.Quote.RiskTier,
            quote.Quote.CreatedAtUtc,
            quote.Quote.ExpiresAtUtc);
        await SaveQuotesAsync(quote);
        await SaveOperationsAsync(operation);

        using var assignRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/operations/assign-to-me",
            "Underwriter",
            "underwriter-1");
        using var assignResponse = await httpClient.SendAsync(assignRequest, TestContext.Current.CancellationToken);

        using var triageRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/operations/triage",
            "Underwriter",
            "underwriter-1",
            new
            {
                priority = "Urgent",
                status = "WaitingForInformation",
                dueAtUtc = new DateTime(2026, 6, 23, 8, 0, 0, DateTimeKind.Utc)
            });
        using var triageResponse = await httpClient.SendAsync(triageRequest, TestContext.Current.CancellationToken);

        using var noteRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/operations/notes",
            "Underwriter",
            "underwriter-1",
            new
            {
                note = "Asked broker team to confirm MFA rollout evidence."
            });
        using var noteResponse = await httpClient.SendAsync(noteRequest, TestContext.Current.CancellationToken);

        using var taskRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/operations/tasks",
            "Underwriter",
            "underwriter-1",
            new
            {
                title = "Verify MFA evidence.",
                dueAtUtc = new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc)
            });
        using var taskResponse = await httpClient.SendAsync(taskRequest, TestContext.Current.CancellationToken);
        var taskContent = await taskResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var taskPayload = JsonDocument.Parse(taskContent);
        var taskId = taskPayload.RootElement.GetProperty("taskId").GetGuid();

        using var completeTaskRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/operations/tasks/{taskId}/complete",
            "Underwriter",
            "underwriter-1");
        using var completeTaskResponse = await httpClient.SendAsync(completeTaskRequest, TestContext.Current.CancellationToken);

        using var timelineRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"{QueueEndpointPath}/{quote.Id}/operations/timeline",
            "Underwriter",
            "underwriter-1");
        using var timelineResponse = await httpClient.SendAsync(timelineRequest, TestContext.Current.CancellationToken);
        var timelineContent = await timelineResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, triageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, noteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, taskResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, completeTaskResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);
        Assert.Contains("AssignmentChanged", timelineContent);
        Assert.Contains("PriorityChanged", timelineContent);
        Assert.Contains("NoteAdded", timelineContent);
        Assert.Contains("TaskAdded", timelineContent);
        Assert.Contains("TaskCompleted", timelineContent);

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedOperation = await dbContext.Set<QuoteReferralOperation>().SingleAsync(
            saved => saved.QuoteId == quote.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal("underwriter-1", savedOperation.AssignedUnderwriterUserId);
        Assert.Equal(ReferralPriority.Urgent, savedOperation.Priority);
        Assert.Equal(ReferralOperationStatus.WaitingForInformation, savedOperation.Status);
    }

    [Fact]
    public async Task Evidence_Request_Workflow_Creates_Owner_Response_And_Underwriter_Acceptance()
    {
        var quote = CreateReferredQuote("customer-1");
        var operation = QuoteReferralOperation.CreateDefault(
            quote.Id,
            quote.Quote.RiskTier,
            quote.Quote.CreatedAtUtc,
            quote.Quote.ExpiresAtUtc);
        await SaveQuotesAsync(quote);
        await SaveOperationsAsync(operation);

        using var createRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/evidence-requests",
            "Underwriter",
            "underwriter-1",
            new
            {
                category = "MultiFactorAuthentication",
                title = "Confirm MFA rollout",
                description = "Please provide current MFA rollout evidence for privileged and email access.",
                dueAtUtc = new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc)
            });
        using var createResponse = await httpClient.SendAsync(createRequest, TestContext.Current.CancellationToken);
        var createContent = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var createPayload = JsonDocument.Parse(createContent);
        var evidenceRequestId = createPayload.RootElement.GetProperty("evidenceRequestId").GetGuid();

        using var ownerListRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/evidence-requests",
            "Customer",
            "customer-1");
        using var ownerListResponse = await httpClient.SendAsync(ownerListRequest, TestContext.Current.CancellationToken);
        var ownerListContent = await ownerListResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var respondRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/evidence-requests/{evidenceRequestId}/respond",
            "Customer",
            "customer-1",
            new
            {
                respondentName = "Jane Applicant",
                respondentTitle = "CISO",
                responseText = "MFA is enforced for all email and privileged accounts.",
                attachmentFileName = "mfa-attestation.pdf",
                attachmentContentType = "application/pdf",
                attachmentSizeBytes = 124_000
            });
        using var respondResponse = await httpClient.SendAsync(respondRequest, TestContext.Current.CancellationToken);

        using var acceptRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/evidence-requests/{evidenceRequestId}/accept",
            "Underwriter",
            "underwriter-1",
            new
            {
                reviewNotes = "MFA evidence is sufficient for this referral."
            });
        using var acceptResponse = await httpClient.SendAsync(acceptRequest, TestContext.Current.CancellationToken);

        using var timelineRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"{QueueEndpointPath}/{quote.Id}/operations/timeline",
            "Underwriter",
            "underwriter-1");
        using var timelineResponse = await httpClient.SendAsync(timelineRequest, TestContext.Current.CancellationToken);
        var timelineContent = await timelineResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerListResponse.StatusCode);
        Assert.Contains("Confirm MFA rollout", ownerListContent);
        Assert.Equal(HttpStatusCode.OK, respondResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);
        Assert.Contains("EvidenceRequestCreated", timelineContent);
        Assert.Contains("EvidenceRequestResponded", timelineContent);
        Assert.Contains("EvidenceRequestAccepted", timelineContent);

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedRequest = await dbContext.Set<QuoteEvidenceRequest>().SingleAsync(
            saved => saved.Id == evidenceRequestId,
            TestContext.Current.CancellationToken);
        var savedOperation = await dbContext.Set<QuoteReferralOperation>().SingleAsync(
            saved => saved.QuoteId == quote.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceRequestStatus.Accepted, savedRequest.Status);
        Assert.Equal("customer-1", savedRequest.OwnerUserId);
        Assert.Equal("mfa-attestation.pdf", savedRequest.AttachmentFileName);
        Assert.Equal(ReferralOperationStatus.WaitingForInformation, savedOperation.Status);
    }

    [Fact]
    public async Task Evidence_Request_Review_Decision_Persists_Audit_And_Exposes_Owner_Remediation()
    {
        var quote = CreateReferredQuote("customer-1");
        var operation = QuoteReferralOperation.CreateDefault(
            quote.Id,
            quote.Quote.RiskTier,
            quote.Quote.CreatedAtUtc,
            quote.Quote.ExpiresAtUtc);
        var evidenceRequest = QuoteEvidenceRequest.Create(
            quote.Id,
            quote.Quote.SubmissionId,
            operation.Id,
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.MultiFactorAuthentication,
            "Confirm MFA rollout",
            "Please provide current MFA rollout evidence for privileged and email access.",
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc));
        evidenceRequest.Respond(
            "customer-1",
            "Jane Applicant",
            "CISO",
            "MFA is enforced for email, but privileged access scope is unclear.",
            null,
            null,
            null,
            new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc));
        await SaveQuotesAsync(quote);
        await SaveOperationsAsync(operation);
        await SaveEvidenceRequestsAsync(evidenceRequest);

        using var reviewRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/evidence-requests/{evidenceRequest.Id}/review-decision",
            "Underwriter",
            "underwriter-2",
            new
            {
                decision = "NeedsClarification",
                reason = "The response does not confirm privileged account MFA scope.",
                remediationGuidance = "Please confirm whether MFA applies to all administrator and service-owner accounts."
            });
        using var reviewResponse = await httpClient.SendAsync(reviewRequest, TestContext.Current.CancellationToken);
        var reviewContent = await reviewResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var ownerListRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/evidence-requests",
            "Customer",
            "customer-1");
        using var ownerListResponse = await httpClient.SendAsync(ownerListRequest, TestContext.Current.CancellationToken);
        var ownerListContent = await ownerListResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var timelineRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"{QueueEndpointPath}/{quote.Id}/operations/timeline",
            "Underwriter",
            "underwriter-2");
        using var timelineResponse = await httpClient.SendAsync(timelineRequest, TestContext.Current.CancellationToken);
        var timelineContent = await timelineResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        Assert.Contains("NeedsClarification", reviewContent);
        Assert.Equal(HttpStatusCode.OK, ownerListResponse.StatusCode);
        Assert.Contains("NeedsClarification", ownerListContent);
        Assert.Contains("Please confirm whether MFA applies to all administrator and service-owner accounts.", ownerListContent);
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);
        Assert.Contains("EvidenceRequestReviewDecisionRecorded", timelineContent);

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedRequest = await dbContext.Set<QuoteEvidenceRequest>().SingleAsync(
            saved => saved.Id == evidenceRequest.Id,
            TestContext.Current.CancellationToken);
        var review = await dbContext.Set<QuoteEvidenceRequestReview>().SingleAsync(
            saved => saved.EvidenceRequestId == evidenceRequest.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceReviewDecisionStatus.NeedsClarification, savedRequest.ReviewDecision);
        Assert.Equal("underwriter-2", savedRequest.ReviewedByUserId);
        Assert.Equal(EvidenceReviewDecisionStatus.NeedsClarification, review.Decision);
        Assert.Equal(0, review.DocumentCount);
        Assert.Equal(0, review.CleanDocumentCount);
    }

    [Fact]
    public async Task Evidence_Request_Review_Decision_Returns_Forbidden_For_Customer()
    {
        var quote = CreateReferredQuote("customer-1");
        var operation = QuoteReferralOperation.CreateDefault(
            quote.Id,
            quote.Quote.RiskTier,
            quote.Quote.CreatedAtUtc,
            quote.Quote.ExpiresAtUtc);
        var evidenceRequest = QuoteEvidenceRequest.Create(
            quote.Id,
            quote.Quote.SubmissionId,
            operation.Id,
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.MultiFactorAuthentication,
            "Confirm MFA rollout",
            "Please provide current MFA rollout evidence.",
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc));
        evidenceRequest.Respond(
            "customer-1",
            "Jane Applicant",
            "CISO",
            "MFA is enforced for all email and privileged accounts.",
            null,
            null,
            null,
            new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc));
        await SaveQuotesAsync(quote);
        await SaveOperationsAsync(operation);
        await SaveEvidenceRequestsAsync(evidenceRequest);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/evidence-requests/{evidenceRequest.Id}/review-decision",
            "Customer",
            "customer-1",
            new
            {
                decision = "Satisfied",
                reason = "Trying to satisfy own evidence request.",
                remediationGuidance = (string?)null
            });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Evidence_Request_Follow_Up_Creates_Timeline_Entry_And_Outbox_Event()
    {
        var quote = CreateReferredQuote("customer-1");
        var operation = QuoteReferralOperation.CreateDefault(
            quote.Id,
            quote.Quote.RiskTier,
            quote.Quote.CreatedAtUtc,
            quote.Quote.ExpiresAtUtc);
        var evidenceRequest = QuoteEvidenceRequest.Create(
            quote.Id,
            quote.Quote.SubmissionId,
            operation.Id,
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.MultiFactorAuthentication,
            "Confirm MFA rollout",
            "Please provide current MFA rollout evidence for privileged and email access.",
            new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 18, 9, 0, 0, DateTimeKind.Utc));
        await SaveQuotesAsync(quote);
        await SaveOperationsAsync(operation);
        await SaveEvidenceRequestsAsync(evidenceRequest);

        using var followUpRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/evidence-requests/{evidenceRequest.Id}/follow-up",
            "Underwriter",
            "underwriter-2");
        using var followUpResponse = await httpClient.SendAsync(followUpRequest, TestContext.Current.CancellationToken);

        using var timelineRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"{QueueEndpointPath}/{quote.Id}/operations/timeline",
            "Underwriter",
            "underwriter-2");
        using var timelineResponse = await httpClient.SendAsync(timelineRequest, TestContext.Current.CancellationToken);
        var timelineContent = await timelineResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, followUpResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);
        Assert.Contains("EvidenceRequestFollowUpSent", timelineContent);

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var outboxMessage = await dbContext.Set<OutboxMessage>().SingleAsync(
            message => message.Type == nameof(QuoteEvidenceRequestFollowUpSentDomainEvent),
            TestContext.Current.CancellationToken);

        Assert.Contains(evidenceRequest.Id.ToString(), outboxMessage.Payload);
        Assert.Contains("underwriter-2", outboxMessage.Payload);
    }

    [Fact]
    public async Task Evidence_Request_Follow_Up_Returns_Forbidden_For_Customer()
    {
        var quote = CreateReferredQuote("customer-1");
        var operation = QuoteReferralOperation.CreateDefault(
            quote.Id,
            quote.Quote.RiskTier,
            quote.Quote.CreatedAtUtc,
            quote.Quote.ExpiresAtUtc);
        var evidenceRequest = QuoteEvidenceRequest.Create(
            quote.Id,
            quote.Quote.SubmissionId,
            operation.Id,
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.MultiFactorAuthentication,
            "Confirm MFA rollout",
            "Please provide current MFA rollout evidence for privileged and email access.",
            new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 18, 9, 0, 0, DateTimeKind.Utc));
        await SaveQuotesAsync(quote);
        await SaveOperationsAsync(operation);
        await SaveEvidenceRequestsAsync(evidenceRequest);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/evidence-requests/{evidenceRequest.Id}/follow-up",
            "Customer",
            "customer-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Evidence_Request_Response_Returns_NotFound_For_Different_Owner()
    {
        var quote = CreateReferredQuote("customer-1");
        var operation = QuoteReferralOperation.CreateDefault(
            quote.Id,
            quote.Quote.RiskTier,
            quote.Quote.CreatedAtUtc,
            quote.Quote.ExpiresAtUtc);
        var evidenceRequest = QuoteEvidenceRequest.Create(
            quote.Id,
            quote.Quote.SubmissionId,
            operation.Id,
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.EndpointDetectionAndResponse,
            "Confirm EDR rollout",
            "Please provide EDR rollout status for managed endpoints.",
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc));
        await SaveQuotesAsync(quote);
        await SaveOperationsAsync(operation);
        await SaveEvidenceRequestsAsync(evidenceRequest);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/evidence-requests/{evidenceRequest.Id}/respond",
            "Customer",
            "customer-2",
            new
            {
                respondentName = "Other User",
                respondentTitle = "CISO",
                responseText = "Trying to respond to another owner's request."
            });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_Quote_Referrals_Returns_Evidence_Summary_For_Underwriter()
    {
        var quote = CreateReferredQuote("customer-1");
        var operation = QuoteReferralOperation.CreateDefault(
            quote.Id,
            quote.Quote.RiskTier,
            quote.Quote.CreatedAtUtc,
            quote.Quote.ExpiresAtUtc);
        var openRequest = QuoteEvidenceRequest.Create(
            quote.Id,
            quote.Quote.SubmissionId,
            operation.Id,
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.BackupRecovery,
            "Confirm backup testing",
            "Please provide latest backup test date.",
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc));
        var respondedRequest = QuoteEvidenceRequest.Create(
            quote.Id,
            quote.Quote.SubmissionId,
            operation.Id,
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.IncidentResponsePlan,
            "Confirm incident response plan",
            "Please provide the latest tabletop exercise notes.",
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc));
        respondedRequest.Respond(
            "customer-1",
            "Jane Applicant",
            "CISO",
            "The latest tabletop exercise was completed in May.",
            null,
            null,
            null,
            new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc));
        await SaveQuotesAsync(quote);
        await SaveOperationsAsync(operation);
        await SaveEvidenceRequestsAsync(openRequest, respondedRequest);

        using var request = CreateAuthenticatedRequest(HttpMethod.Get, QueueEndpointPath, "Underwriter", "underwriter-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var evidence = payload.RootElement
            .GetProperty("quoteReferrals")[0]
            .GetProperty("evidence");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, evidence.GetProperty("openRequestCount").GetInt32());
        Assert.Equal(1, evidence.GetProperty("respondedRequestCount").GetInt32());
        Assert.True(evidence.GetProperty("isWaitingForInformation").GetBoolean());
        Assert.Equal(
            new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc),
            evidence.GetProperty("latestEvidenceActivityAtUtc").GetDateTime());
        Assert.Equal(0, evidence.GetProperty("overdueRequestCount").GetInt32());
        Assert.Equal(
            new DateTime(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc),
            evidence.GetProperty("nextOpenDueAtUtc").GetDateTime());
    }

    [Fact]
    public async Task List_Quote_Referrals_Counts_Only_Open_Overdue_Evidence_Requests()
    {
        var quote = CreateReferredQuote("customer-1");
        var operation = QuoteReferralOperation.CreateDefault(
            quote.Id,
            quote.Quote.RiskTier,
            quote.Quote.CreatedAtUtc,
            quote.Quote.ExpiresAtUtc);
        var overdueOpenRequest = QuoteEvidenceRequest.Create(
            quote.Id,
            quote.Quote.SubmissionId,
            operation.Id,
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.BackupRecovery,
            "Confirm backup testing",
            "Please provide latest backup test date.",
            new DateTime(2020, 6, 20, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 6, 18, 9, 0, 0, DateTimeKind.Utc));
        var overdueRespondedRequest = QuoteEvidenceRequest.Create(
            quote.Id,
            quote.Quote.SubmissionId,
            operation.Id,
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.IncidentResponsePlan,
            "Confirm incident response plan",
            "Please provide the latest tabletop exercise notes.",
            new DateTime(2020, 6, 20, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 6, 18, 10, 0, 0, DateTimeKind.Utc));
        overdueRespondedRequest.Respond(
            "customer-1",
            "Jane Applicant",
            "CISO",
            "The latest tabletop exercise was completed in May.",
            null,
            null,
            null,
            new DateTime(2020, 6, 19, 12, 0, 0, DateTimeKind.Utc));
        await SaveQuotesAsync(quote);
        await SaveOperationsAsync(operation);
        await SaveEvidenceRequestsAsync(overdueOpenRequest, overdueRespondedRequest);

        using var request = CreateAuthenticatedRequest(HttpMethod.Get, QueueEndpointPath, "Underwriter", "underwriter-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var evidence = payload.RootElement
            .GetProperty("quoteReferrals")[0]
            .GetProperty("evidence");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, evidence.GetProperty("overdueRequestCount").GetInt32());
        Assert.Equal(
            new DateTime(2020, 6, 20, 9, 0, 0, DateTimeKind.Utc),
            evidence.GetProperty("nextOpenDueAtUtc").GetDateTime());
    }

    [Fact]
    public async Task List_Owner_Evidence_Requests_Returns_Overdue_And_Days_Until_Due()
    {
        var quote = CreateReferredQuote("customer-1");
        var operation = QuoteReferralOperation.CreateDefault(
            quote.Id,
            quote.Quote.RiskTier,
            quote.Quote.CreatedAtUtc,
            quote.Quote.ExpiresAtUtc);
        var evidenceRequest = QuoteEvidenceRequest.Create(
            quote.Id,
            quote.Quote.SubmissionId,
            operation.Id,
            "customer-1",
            "underwriter-1",
            EvidenceRequestCategory.BackupRecovery,
            "Confirm backup testing",
            "Please provide latest backup test date.",
            new DateTime(2020, 6, 20, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 6, 18, 9, 0, 0, DateTimeKind.Utc));
        await SaveQuotesAsync(quote);
        await SaveOperationsAsync(operation);
        await SaveEvidenceRequestsAsync(evidenceRequest);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/evidence-requests",
            "Customer",
            "customer-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var evidence = payload.RootElement.GetProperty("evidenceRequests")[0];

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(evidence.GetProperty("isOverdue").GetBoolean());
        Assert.True(evidence.GetProperty("daysUntilDue").GetInt32() < 0);
    }

    [Fact]
    public async Task Approve_Quote_Referral_Returns_Forbidden_For_Customer()
    {
        var quote = CreateReferredQuote("customer-1");
        await SaveQuotesAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/approve",
            "Customer",
            "customer-1",
            new
            {
                reason = "Trying to approve own referred quote.",
                notes = "Should be forbidden."
            });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Operations_Action_Returns_Forbidden_For_Customer()
    {
        var quote = CreateReferredQuote("customer-1");
        await SaveQuotesAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/operations/assign-to-me",
            "Customer",
            "customer-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approve_Quote_Referral_Persists_Decision_Audit_Row_And_Outbox_Event()
    {
        var quote = CreateReferredQuote("customer-1");
        await SaveQuotesAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/approve",
            "Underwriter",
            "underwriter-1",
            new
            {
                reason = "Controls are acceptable after manual review.",
                notes = "MFA evidence reviewed."
            });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Approved", root.GetProperty("status").GetString());
        Assert.Equal("underwriter-1", root.GetProperty("reviewedByUserId").GetString());

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedQuote = await dbContext.Quotes.SingleAsync(
            saved => saved.Id == quote.Id,
            TestContext.Current.CancellationToken);
        var review = await dbContext.Set<QuoteUnderwritingReview>().SingleAsync(
            saved => saved.QuoteId == quote.Id,
            TestContext.Current.CancellationToken);
        var outboxMessage = await dbContext.Set<OutboxMessage>().SingleAsync(
            message => message.Type == nameof(QuoteUnderwritingDecisionRecordedDomainEvent),
            TestContext.Current.CancellationToken);

        Assert.Equal(QuoteStatus.Approved, savedQuote.Status);
        Assert.Equal("underwriter-1", savedQuote.ReviewedByUserId);
        Assert.Equal("Controls are acceptable after manual review.", savedQuote.UnderwritingDecisionReason);
        Assert.Equal(QuoteUnderwritingDecision.Approved, review.Decision);
        Assert.Equal(quote.Premium, review.PremiumBefore);
        Assert.Equal(quote.Premium, review.PremiumAfter);
        Assert.Contains(quote.Id.ToString(), outboxMessage.Payload);
    }

    [Fact]
    public async Task Decline_Quote_Referral_Prevents_Later_Adjustment()
    {
        var quote = CreateReferredQuote("customer-1");
        await SaveQuotesAsync(quote);

        using var declineRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/decline",
            "Underwriter",
            "underwriter-1",
            new
            {
                reason = "Risk is outside current appetite.",
                notes = "Too many unresolved controls."
            });
        using var declineResponse = await httpClient.SendAsync(declineRequest, TestContext.Current.CancellationToken);

        using var adjustRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/adjust",
            "Underwriter",
            "underwriter-1",
            new
            {
                adjustedPremium = 25_000m,
                adjustedRetention = 25_000m,
                reason = "Trying to adjust after decline.",
                notes = "Should fail.",
                updatedSubjectivities = "Updated subjectivity."
            });
        using var adjustResponse = await httpClient.SendAsync(adjustRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, declineResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, adjustResponse.StatusCode);

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedQuote = await dbContext.Quotes.SingleAsync(
            saved => saved.Id == quote.Id,
            TestContext.Current.CancellationToken);
        var reviewCount = await dbContext.Set<QuoteUnderwritingReview>().CountAsync(
            saved => saved.QuoteId == quote.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(QuoteStatus.Declined, savedQuote.Status);
        Assert.Equal(1, reviewCount);
    }

    [Fact]
    public async Task Operations_Mutation_Returns_Conflict_After_Final_Underwriting_Decision()
    {
        var quote = CreateReferredQuote("customer-1");
        var operation = QuoteReferralOperation.CreateDefault(
            quote.Id,
            quote.Quote.RiskTier,
            quote.Quote.CreatedAtUtc,
            quote.Quote.ExpiresAtUtc);
        await SaveQuotesAsync(quote);
        await SaveOperationsAsync(operation);

        using var approveRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/approve",
            "Underwriter",
            "underwriter-1",
            new
            {
                reason = "Controls are acceptable after manual review.",
                notes = "MFA evidence reviewed."
            });
        using var approveResponse = await httpClient.SendAsync(approveRequest, TestContext.Current.CancellationToken);

        using var noteRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/operations/notes",
            "Underwriter",
            "underwriter-1",
            new
            {
                note = "Trying to add note after approval."
            });
        using var noteResponse = await httpClient.SendAsync(noteRequest, TestContext.Current.CancellationToken);

        using var timelineRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"{QueueEndpointPath}/{quote.Id}/operations/timeline",
            "Underwriter",
            "underwriter-1");
        using var timelineResponse = await httpClient.SendAsync(timelineRequest, TestContext.Current.CancellationToken);
        var timelineContent = await timelineResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, noteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);
        Assert.Contains("DecisionRecorded", timelineContent);
    }

    [Fact]
    public async Task Adjust_Quote_Referral_Updates_Terms_And_Stores_Audit_Before_And_After_Values()
    {
        var quote = CreateReferredQuote("customer-1");
        await SaveQuotesAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/adjust",
            "Admin",
            "admin-1",
            new
            {
                adjustedPremium = 24_000m,
                adjustedRetention = 50_000m,
                reason = "Adjusted for stronger reviewed controls.",
                notes = "Admin approval.",
                updatedSubjectivities = "Evidence of MFA and EDR required before bind."
            });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedQuote = await dbContext.Quotes.SingleAsync(
            saved => saved.Id == quote.Id,
            TestContext.Current.CancellationToken);
        var review = await dbContext.Set<QuoteUnderwritingReview>().SingleAsync(
            saved => saved.QuoteId == quote.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(QuoteStatus.Approved, savedQuote.Status);
        Assert.Equal(24_000m, savedQuote.Premium);
        Assert.Equal(50_000m, savedQuote.Retention);
        Assert.Equal("Evidence of MFA and EDR required before bind.", savedQuote.Subjectivities);
        Assert.Equal(QuoteUnderwritingDecision.Adjusted, review.Decision);
        Assert.Equal(quote.Premium, review.PremiumBefore);
        Assert.Equal(24_000m, review.PremiumAfter);
        Assert.Equal(quote.Retention, review.RetentionBefore);
        Assert.Equal(50_000m, review.RetentionAfter);
    }

    [Fact]
    public async Task Approve_Quote_Referral_Returns_Conflict_For_Non_Referred_Quote()
    {
        var quote = CreateQuotedQuote("customer-1");
        await SaveQuotesAsync(quote);

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{QueueEndpointPath}/{quote.Id}/approve",
            "Underwriter",
            "underwriter-1",
            new
            {
                reason = "Trying to review a quote that was not referred.",
                notes = "Should fail."
            });
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
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

    private async Task SaveQuotesAsync(params SeededQuote[] quotes)
    {
        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        await dbContext.Submissions.AddRangeAsync(
            quotes.Select(quote => quote.Submission),
            TestContext.Current.CancellationToken);
        await dbContext.Quotes.AddRangeAsync(
            quotes.Select(quote => quote.Quote),
            TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SaveOperationsAsync(params QuoteReferralOperation[] operations)
    {
        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        await dbContext.Set<QuoteReferralOperation>().AddRangeAsync(
            operations,
            TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SaveEvidenceRequestsAsync(params QuoteEvidenceRequest[] evidenceRequests)
    {
        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        await dbContext.Set<QuoteEvidenceRequest>().AddRangeAsync(
            evidenceRequests,
            TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static SeededQuote CreateReferredQuote(
        string ownerUserId,
        DateTime? createdAtUtc = null)
    {
        var submission = CreateSubmittedSubmission(ownerUserId, createdAtUtc);
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
            createdAtUtc ?? new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc));

        return new SeededQuote(submission, quote);
    }

    private static SeededQuote CreateQuotedQuote(
        string ownerUserId,
        DateTime? createdAtUtc = null)
    {
        var submission = CreateSubmittedSubmission(ownerUserId, createdAtUtc);
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
            createdAtUtc ?? new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc));

        return new SeededQuote(submission, quote);
    }

    private static Submission CreateSubmittedSubmission(string ownerUserId, DateTime? createdAtUtc = null)
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            ownerUserId,
            createdAtUtc ?? new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc));
        submission.Submit();
        submission.ClearDomainEvents();

        return submission;
    }

    private sealed record SeededQuote(Submission Submission, Quote Quote)
    {
        public Guid Id => Quote.Id;

        public decimal Premium => Quote.Premium;

        public decimal Retention => Quote.Retention;
    }

    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
        databaseConnection.Dispose();
    }
}
