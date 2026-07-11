using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LIAnsureProtect.Application.Quotes.RatingProviders;
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


public sealed class SubmissionEndpointTests
    : IClassFixture<WebApplicationFactory<Program>>,
      IDisposable
{
    private const string SubmissionsEndpointPath = "/api/v1/submissions";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");

    private readonly SqliteConnection databaseConnection;
    private readonly WebApplicationFactory<Program> webApplicationFactory;
    private readonly HttpClient httpClient;


    public SubmissionEndpointTests(WebApplicationFactory<Program> webApplicationFactory)
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
                        options => { }
                    );

                services.RemoveAll<IRatingProviderClient>();
                services.AddSingleton<TestRatingProviderClient>();
                services.AddSingleton<IRatingProviderClient>(
                    serviceProvider => serviceProvider.GetRequiredService<TestRatingProviderClient>());
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



    private static HttpRequestMessage CreateAuthenticatedPostRequest(
        string role,
        object body,
        string userId = "test-user-1")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, SubmissionsEndpointPath)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.EmailHeader, "test-user@example.com");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);

        return request;
    }



    private static HttpRequestMessage CreateAuthenticatedPostRequest(
        string role,
        object body,
        string userId,
        string idempotencyKey)
    {
        var request = CreateAuthenticatedPostRequest(role, body, userId);
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        return request;
    }



    private static HttpRequestMessage CreateAuthenticatedPostRequest(
        string role,
        string path,
        string userId = "test-user-1")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.EmailHeader, "test-user@example.com");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);

        return request;
    }



    private static HttpRequestMessage CreateAuthenticatedPostRequest(
        string role,
        string path,
        string userId,
        string idempotencyKey)
    {
        var request = CreateAuthenticatedPostRequest(role, path, userId);
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        return request;
    }



    private static HttpRequestMessage CreateAuthenticatedPostRequest(
        string role,
        string path,
        object body,
        string userId = "test-user-1")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.EmailHeader, "test-user@example.com");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);

        return request;
    }



    private static HttpRequestMessage CreateAuthenticatedPostRequest(
        string role,
        string path,
        object body,
        string userId,
        string idempotencyKey)
    {
        var request = CreateAuthenticatedPostRequest(role, path, body, userId);
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        return request;
    }



    private static HttpRequestMessage CreateAuthenticatedGetRequest(
        string role,
        string path,
        string userId = "test-user-1")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.EmailHeader, "test-user@example.com");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);

        return request;
    }

    private static HttpRequestMessage CreateAuthenticatedPutRequest(
        string role,
        string path,
        object body,
        string userId = "test-user-1")
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.EmailHeader, "test-user@example.com");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);

        return request;
    }



    [Fact]
    public async Task Create_Submission_Returns_Created_Draft_Submission()
    {
        var request = new
        {
            applicantName = "Jane Applicant",
            applicantEmail = "jane@example.com",
            companyName = "Example Company"
        };

        using var httpRequest = CreateAuthenticatedPostRequest("Customer", request);
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;
        var submissionId = root.GetProperty("submissionId").GetGuid();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(Guid.Empty, submissionId);
        Assert.Equal("Draft", root.GetProperty("status").GetString());
        Assert.Equal($"/api/v1/submissions/{submissionId}", response.Headers.Location?.OriginalString);

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedSubmission = await dbContext.Submissions.SingleAsync(
            submission => submission.Id == submissionId,
            TestContext.Current.CancellationToken
        );

        Assert.Equal("Jane Applicant", savedSubmission.ApplicantName);
        Assert.Equal("jane@example.com", savedSubmission.ApplicantEmail);
        Assert.Equal("Example Company", savedSubmission.CompanyName);
        Assert.Equal("test-user-1", savedSubmission.OwnerUserId);
    }



    [Fact]
    public async Task Create_Submission_Returns_Bad_Request_For_Invalid_Input()
    {
        var request = new
        {
            applicantName = string.Empty,
            applicantEmail = "not-an-email",
            companyName = string.Empty
        };

        using var httpRequest = CreateAuthenticatedPostRequest("Customer", request);
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var errors = payload.RootElement.GetProperty("errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(errors.TryGetProperty("ApplicantName", out _));
        Assert.True(errors.TryGetProperty("ApplicantEmail", out _));
        Assert.True(errors.TryGetProperty("CompanyName", out _));
    }



    [Fact]
    public async Task Create_Submission_Returns_Unauthorized_For_Anonymous_User()
    {
        var request = new
        {
            applicantName = "Jane Applicant",
            applicantEmail = "jane@example.com",
            companyName = "Example Company"
        };

        // The request has no X-Test-UserId, so TestAuthHandler returns no authenticated user.
        using var response = await httpClient.PostAsJsonAsync(SubmissionsEndpointPath, request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }



    [Fact]
    public async Task Create_Submission_Returns_Forbidden_For_Unauthorized_Role()
    {
        var request = new
        {
            applicantName = "Jane Applicant",
            applicantEmail = "jane@example.com",
            companyName = "Example Company"
        };

        // An Underwriter is authenticated, but Submissions.Create only allows these roles: Customer, Broker, and Admin
        using var httpRequest = CreateAuthenticatedPostRequest("Underwriter", request);
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }



    [Theory]
    [InlineData("Customer")]
    [InlineData("Broker")]
    [InlineData("Admin")]
    public async Task Create_Submission_Returns_Created_For_Authorized_Roles(string role)
    {
        var request = new
        {
            applicantName = $"{role} Applicant",
            applicantEmail = $"{role.ToLowerInvariant()}@example.com",
            companyName = $"{role} Company"
        };

        using var httpRequest = CreateAuthenticatedPostRequest(role, request);
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }



    [Fact]
    public async Task Create_Submission_With_Idempotency_Key_Returns_Same_Response_And_Creates_One_Submission()
    {
        var request = new
        {
            applicantName = "Jane Applicant",
            applicantEmail = "jane@example.com",
            companyName = "Example Company"
        };
        var idempotencyKey = "create-submission-key-1";

        using var firstRequest = CreateAuthenticatedPostRequest(
            "Customer",
            request,
            "test-user-1",
            idempotencyKey);
        using var firstResponse = await httpClient.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        var firstContent = await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var secondRequest = CreateAuthenticatedPostRequest(
            "Customer",
            request,
            "test-user-1",
            idempotencyKey);
        using var secondResponse = await httpClient.SendAsync(secondRequest, TestContext.Current.CancellationToken);
        var secondContent = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var firstPayload = JsonDocument.Parse(firstContent);
        using var secondPayload = JsonDocument.Parse(secondContent);
        var firstSubmissionId = firstPayload.RootElement.GetProperty("submissionId").GetGuid();
        var secondSubmissionId = secondPayload.RootElement.GetProperty("submissionId").GetGuid();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(firstSubmissionId, secondSubmissionId);
        Assert.Equal(firstContent, secondContent);
        Assert.Equal(firstResponse.Headers.Location?.OriginalString, secondResponse.Headers.Location?.OriginalString);

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedSubmissionCount = await dbContext.Submissions.CountAsync(
            submission => submission.OwnerUserId == "test-user-1",
            TestContext.Current.CancellationToken);

        Assert.Equal(1, savedSubmissionCount);
    }



    [Fact]
    public async Task Create_Submission_Returns_Conflict_When_Idempotency_Key_Is_Reused_With_Different_Body()
    {
        var firstRequestBody = new
        {
            applicantName = "Jane Applicant",
            applicantEmail = "jane@example.com",
            companyName = "Example Company"
        };
        var secondRequestBody = new
        {
            applicantName = "Different Applicant",
            applicantEmail = "different@example.com",
            companyName = "Different Company"
        };
        var idempotencyKey = "create-submission-key-2";

        using var firstRequest = CreateAuthenticatedPostRequest(
            "Customer",
            firstRequestBody,
            "test-user-1",
            idempotencyKey);
        using var firstResponse = await httpClient.SendAsync(firstRequest, TestContext.Current.CancellationToken);

        using var secondRequest = CreateAuthenticatedPostRequest(
            "Customer",
            secondRequestBody,
            "test-user-1",
            idempotencyKey);
        using var secondResponse = await httpClient.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        using var scope = webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedSubmissionCount = await dbContext.Submissions.CountAsync(
            submission => submission.OwnerUserId == "test-user-1",
            TestContext.Current.CancellationToken);

        Assert.Equal(1, savedSubmissionCount);
    }



    [Fact]
    public async Task List_Submissions_Returns_Unauthorized_For_Anonymous_User()
    {
        using var response = await httpClient.GetAsync(
            SubmissionsEndpointPath,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }



    [Fact]
    public async Task List_Submissions_Returns_Saved_Submissions_For_Authorized_User()
    {
        var olderSubmission = Submission.CreateDraft(
            "Older Applicant",
            "older@example.com",
            "Older Company",
            "test-user-1",
            new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc));
        var newerSubmission = Submission.CreateDraft(
            "Newer Applicant",
            "newer@example.com",
            "Newer Company",
            "test-user-1",
            new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc));

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddRangeAsync(
                [olderSubmission, newerSubmission],
                TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedGetRequest("Customer", SubmissionsEndpointPath);
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var submissions = payload.RootElement.GetProperty("submissions").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, submissions.Length);
        Assert.Equal(newerSubmission.Id, submissions[0].GetProperty("submissionId").GetGuid());
        Assert.Equal("Newer Applicant", submissions[0].GetProperty("applicantName").GetString());
        Assert.Equal("newer@example.com", submissions[0].GetProperty("applicantEmail").GetString());
        Assert.Equal("Newer Company", submissions[0].GetProperty("companyName").GetString());
        Assert.Equal("Draft", submissions[0].GetProperty("status").GetString());
        Assert.Equal(olderSubmission.Id, submissions[1].GetProperty("submissionId").GetGuid());
    }



    [Fact]
    public async Task List_Submissions_Returns_Only_Submissions_Owned_By_Current_User()
    {
        var currentUserSubmission = Submission.CreateDraft(
            "Current User Applicant",
            "current@example.com",
            "Current User Company",
            "test-user-1",
            new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc));
        var otherUserSubmission = Submission.CreateDraft(
            "Other User Applicant",
            "other@example.com",
            "Other User Company",
            "test-user-2",
            new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc));

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddRangeAsync(
                [currentUserSubmission, otherUserSubmission],
                TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedGetRequest("Customer", SubmissionsEndpointPath, "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var submissions = payload.RootElement.GetProperty("submissions").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var submission = Assert.Single(submissions);
        Assert.Equal(currentUserSubmission.Id, submission.GetProperty("submissionId").GetGuid());
        Assert.Equal("Current User Applicant", submission.GetProperty("applicantName").GetString());
    }



    [Fact]
    public async Task Get_Submission_Detail_Returns_Unauthorized_For_Anonymous_User()
    {
        var submissionId = Guid.Parse("af1453a4-0b68-4432-99d9-becb456a1001");

        using var response = await httpClient.GetAsync(
            $"{SubmissionsEndpointPath}/{submissionId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }



    [Fact]
    public async Task Get_Submission_Detail_Returns_Saved_Submission_For_Authorized_User()
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "test-user-1",
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedGetRequest("Customer", $"{SubmissionsEndpointPath}/{submission.Id}");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(submission.Id, root.GetProperty("submissionId").GetGuid());
        Assert.Equal("Jane Applicant", root.GetProperty("applicantName").GetString());
        Assert.Equal("jane@example.com", root.GetProperty("applicantEmail").GetString());
        Assert.Equal("Example Company", root.GetProperty("companyName").GetString());
        Assert.Equal("Draft", root.GetProperty("status").GetString());
    }



    [Fact]
    public async Task Get_Submission_Detail_Returns_Latest_Quote_For_Authorized_User()
    {
        var submission = CreateSubmittedSubmission("test-user-1");
        var quote = Quote.Generate(
            submission.Id,
            "test-user-1",
            6_500m,
            1_000_000m,
            10_000m,
            CyberRiskTier.Low,
            "BaselineCyber",
            ["Maintain MFA for privileged accounts."],
            [],
            new DateTime(2026, 6, 19, 8, 45, 0, DateTimeKind.Utc));
        quote.ClearDomainEvents();

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.Quotes.AddAsync(quote, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedGetRequest("Customer", $"{SubmissionsEndpointPath}/{submission.Id}");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var latestQuote = payload.RootElement.GetProperty("latestQuote");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(quote.Id, latestQuote.GetProperty("quoteId").GetGuid());
        Assert.Equal("Quoted", latestQuote.GetProperty("status").GetString());
        Assert.Equal("Low", latestQuote.GetProperty("riskTier").GetString());
        Assert.Equal(6_500m, latestQuote.GetProperty("premium").GetDecimal());
        Assert.Contains(
            latestQuote.GetProperty("subjectivities").EnumerateArray(),
            subjectivity => subjectivity.GetString() == "Maintain MFA for privileged accounts.");
    }



    [Fact]
    public async Task Get_Submission_Detail_Returns_Not_Found_For_Missing_Submission()
    {
        var submissionId = Guid.Parse("c43e4434-6b30-4d52-a38b-b2d24f8a1002");

        using var httpRequest = CreateAuthenticatedGetRequest("Customer", $"{SubmissionsEndpointPath}/{submissionId}");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }



    [Fact]
    public async Task Get_Submission_Detail_Returns_Not_Found_For_Submission_Owned_By_Different_User()
    {
        var otherUserSubmission = Submission.CreateDraft(
            "Other User Applicant",
            "other@example.com",
            "Other User Company",
            "test-user-2",
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(otherUserSubmission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedGetRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{otherUserSubmission.Id}",
            "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_Submission_Returns_Ok_And_Updates_Owned_Draft_Submission()
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "test-user-1",
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));
        var requestBody = new
        {
            applicantName = "Updated Applicant",
            applicantEmail = "updated@example.com",
            companyName = "Updated Company"
        };

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedPutRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}",
            requestBody,
            "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(submission.Id, root.GetProperty("submissionId").GetGuid());
        Assert.Equal("Updated Applicant", root.GetProperty("applicantName").GetString());
        Assert.Equal("updated@example.com", root.GetProperty("applicantEmail").GetString());
        Assert.Equal("Updated Company", root.GetProperty("companyName").GetString());
        Assert.Equal("Draft", root.GetProperty("status").GetString());

        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedSubmission = await verifyDbContext.Submissions.SingleAsync(
            saved => saved.Id == submission.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal("Updated Applicant", savedSubmission.ApplicantName);
        Assert.Equal("updated@example.com", savedSubmission.ApplicantEmail);
        Assert.Equal("Updated Company", savedSubmission.CompanyName);
        Assert.Equal(SubmissionStatus.Draft, savedSubmission.Status);
    }

    [Fact]
    public async Task Update_Submission_Returns_Bad_Request_For_Invalid_Input()
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "test-user-1",
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));
        var requestBody = new
        {
            applicantName = string.Empty,
            applicantEmail = "not-an-email",
            companyName = string.Empty
        };

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedPutRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}",
            requestBody,
            "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var errors = payload.RootElement.GetProperty("errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(errors.TryGetProperty("ApplicantName", out _));
        Assert.True(errors.TryGetProperty("ApplicantEmail", out _));
        Assert.True(errors.TryGetProperty("CompanyName", out _));
    }

    [Fact]
    public async Task Update_Submission_Returns_Not_Found_For_Submission_Owned_By_Different_User()
    {
        var otherUserSubmission = Submission.CreateDraft(
            "Other User Applicant",
            "other@example.com",
            "Other User Company",
            "test-user-2",
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));
        var requestBody = new
        {
            applicantName = "Updated Applicant",
            applicantEmail = "updated@example.com",
            companyName = "Updated Company"
        };

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(otherUserSubmission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedPutRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{otherUserSubmission.Id}",
            requestBody,
            "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_Submission_Returns_Conflict_For_Submitted_Submission()
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "test-user-1",
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));
        submission.Submit();
        var requestBody = new
        {
            applicantName = "Updated Applicant",
            applicantEmail = "updated@example.com",
            companyName = "Updated Company"
        };

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedPutRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}",
            requestBody,
            "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedSubmission = await verifyDbContext.Submissions.SingleAsync(
            saved => saved.Id == submission.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal("Jane Applicant", savedSubmission.ApplicantName);
        Assert.Equal("jane@example.com", savedSubmission.ApplicantEmail);
        Assert.Equal("Example Company", savedSubmission.CompanyName);
        Assert.Equal(SubmissionStatus.Submitted, savedSubmission.Status);
    }



    [Fact]
    public async Task Submit_Submission_Returns_Unauthorized_For_Anonymous_User()
    {
        var submissionId = Guid.Parse("cf94d3bf-2830-43ce-8aa2-2a01636c5d78");

        using var response = await httpClient.PostAsync(
            $"{SubmissionsEndpointPath}/{submissionId}/submit",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }



    [Fact]
    public async Task Submit_Submission_Returns_Forbidden_For_Unauthorized_Role()
    {
        var submissionId = Guid.Parse("de40f4a0-48db-4920-aae7-8de3150b5e84");

        using var httpRequest = CreateAuthenticatedPostRequest(
            "Underwriter",
            $"{SubmissionsEndpointPath}/{submissionId}/submit");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }



    [Fact]
    public async Task Submit_Submission_Returns_Ok_And_Updates_Owned_Draft_Submission()
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "test-user-1",
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/submit",
            "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(submission.Id, root.GetProperty("submissionId").GetGuid());
        Assert.Equal("Submitted", root.GetProperty("status").GetString());

        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedSubmission = await verifyDbContext.Submissions.SingleAsync(
            saved => saved.Id == submission.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(SubmissionStatus.Submitted, savedSubmission.Status);
    }



    [Fact]
    public async Task Submit_Submission_Persists_SubmissionSubmitted_Outbox_Message()
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "test-user-1",
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/submit",
            "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var outboxMessage = await verifyDbContext.Set<OutboxMessage>().SingleAsync(
            message => message.Type.Contains(nameof(SubmissionSubmittedDomainEvent)),
            TestContext.Current.CancellationToken);

        Assert.Equal(nameof(SubmissionSubmittedDomainEvent), outboxMessage.Type);
        Assert.Null(outboxMessage.ProcessedAtUtc);
        Assert.Null(outboxMessage.Error);
        Assert.Contains(submission.Id.ToString(), outboxMessage.Payload);
        Assert.Contains("test-user-1", outboxMessage.Payload);
    }



    [Fact]
    public async Task Submit_Submission_With_Idempotency_Key_Returns_Same_Response_And_Creates_One_Outbox_Message()
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "test-user-1",
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));
        var idempotencyKey = "submit-submission-key-1";

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var firstRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/submit",
            "test-user-1",
            idempotencyKey);
        using var firstResponse = await httpClient.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        var firstContent = await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var secondRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/submit",
            "test-user-1",
            idempotencyKey);
        using var secondResponse = await httpClient.SendAsync(secondRequest, TestContext.Current.CancellationToken);
        var secondContent = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstContent, secondContent);

        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var outboxMessageCount = await verifyDbContext.Set<OutboxMessage>().CountAsync(
            message => message.Type == nameof(SubmissionSubmittedDomainEvent),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, outboxMessageCount);
    }



    [Fact]
    public async Task Submit_Submission_Returns_Conflict_When_Idempotency_Key_Is_Reused_By_Different_User()
    {
        var firstUserSubmission = Submission.CreateDraft(
            "First User Applicant",
            "first@example.com",
            "First User Company",
            "test-user-1",
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));
        var secondUserSubmission = Submission.CreateDraft(
            "Second User Applicant",
            "second@example.com",
            "Second User Company",
            "test-user-2",
            new DateTime(2026, 6, 19, 8, 35, 0, DateTimeKind.Utc));
        var idempotencyKey = "submit-submission-key-2";

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddRangeAsync(
                [firstUserSubmission, secondUserSubmission],
                TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var firstRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{firstUserSubmission.Id}/submit",
            "test-user-1",
            idempotencyKey);
        using var firstResponse = await httpClient.SendAsync(firstRequest, TestContext.Current.CancellationToken);

        using var secondRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{secondUserSubmission.Id}/submit",
            "test-user-2",
            idempotencyKey);
        using var secondResponse = await httpClient.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedSecondSubmission = await verifyDbContext.Submissions.SingleAsync(
            submission => submission.Id == secondUserSubmission.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(SubmissionStatus.Draft, savedSecondSubmission.Status);
    }



    [Fact]
    public async Task Submit_Submission_Returns_Conflict_When_Idempotency_Key_Is_Reused_For_Different_Action()
    {
        var createRequestBody = new
        {
            applicantName = "Jane Applicant",
            applicantEmail = "jane@example.com",
            companyName = "Example Company"
        };
        var idempotencyKey = "shared-action-key-1";

        using var createRequest = CreateAuthenticatedPostRequest(
            "Customer",
            createRequestBody,
            "test-user-1",
            idempotencyKey);
        using var createResponse = await httpClient.SendAsync(createRequest, TestContext.Current.CancellationToken);
        var createContent = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var createPayload = JsonDocument.Parse(createContent);
        var submissionId = createPayload.RootElement.GetProperty("submissionId").GetGuid();

        using var submitRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submissionId}/submit",
            "test-user-1",
            idempotencyKey);
        using var submitResponse = await httpClient.SendAsync(submitRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, submitResponse.StatusCode);

        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedSubmission = await verifyDbContext.Submissions.SingleAsync(
            submission => submission.Id == submissionId,
            TestContext.Current.CancellationToken);

        Assert.Equal(SubmissionStatus.Draft, savedSubmission.Status);
    }



    [Fact]
    public async Task Submit_Submission_Returns_Not_Found_For_Submission_Owned_By_Different_User()
    {
        var otherUserSubmission = Submission.CreateDraft(
            "Other User Applicant",
            "other@example.com",
            "Other User Company",
            "test-user-2",
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(otherUserSubmission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{otherUserSubmission.Id}/submit",
            "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedSubmission = await verifyDbContext.Submissions.SingleAsync(
            saved => saved.Id == otherUserSubmission.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(SubmissionStatus.Draft, savedSubmission.Status);
    }



    [Fact]
    public async Task Submit_Submission_Returns_Conflict_For_Repeated_Submit()
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "test-user-1",
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));
        submission.Submit();

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/submit",
            "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Draft_Removes_Owned_Draft_Only()
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "test-user-1",
            DateTime.UtcNow);
        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{SubmissionsEndpointPath}/{submission.Id}");
        request.Headers.Add(TestAuthHandler.UserIdHeader, "test-user-1");
        request.Headers.Add(TestAuthHandler.EmailHeader, "test-user@example.com");
        request.Headers.Add(TestAuthHandler.RolesHeader, "Customer");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        Assert.False(await verifyDbContext.Submissions.AnyAsync(
            saved => saved.Id == submission.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_Submitted_Submission_Returns_Conflict_And_Retains_History()
    {
        var submission = CreateSubmittedSubmission("test-user-1");
        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{SubmissionsEndpointPath}/{submission.Id}");
        request.Headers.Add(TestAuthHandler.UserIdHeader, "test-user-1");
        request.Headers.Add(TestAuthHandler.EmailHeader, "test-user@example.com");
        request.Headers.Add(TestAuthHandler.RolesHeader, "Customer");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        Assert.True(await verifyDbContext.Submissions.AnyAsync(
            saved => saved.Id == submission.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Withdraw_Submitted_Submission_Is_Idempotent_And_Writes_One_Audit_Event()
    {
        var submission = CreateSubmittedSubmission("test-user-1");
        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var firstRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/withdraw",
            "test-user-1");
        using var firstResponse = await httpClient.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        using var secondRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/withdraw",
            "test-user-1");
        using var secondResponse = await httpClient.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        Assert.Equal(SubmissionStatus.Withdrawn, (await verifyDbContext.Submissions.SingleAsync(
            saved => saved.Id == submission.Id,
            TestContext.Current.CancellationToken)).Status);
        Assert.Equal(1, await verifyDbContext.OutboxMessages.CountAsync(
            message => message.Type == nameof(SubmissionWithdrawnDomainEvent),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Withdraw_Submission_Returns_Conflict_After_Quote_Acceptance()
    {
        var submission = CreateSubmittedSubmission("test-user-1");
        var quote = Quote.Generate(
            submission.Id,
            "test-user-1",
            12_000m,
            1_000_000m,
            10_000m,
            CyberRiskTier.Moderate,
            "BaselineCyber",
            ["Maintain MFA."],
            [],
            DateTime.UtcNow);
        quote.Accept(
            "test-user-1",
            "Jane Applicant",
            "CFO",
            true,
            DateTime.UtcNow);
        quote.ClearDomainEvents();
        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.Quotes.AddAsync(quote, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var request = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/withdraw",
            "test-user-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        Assert.Equal(SubmissionStatus.Submitted, (await verifyDbContext.Submissions.SingleAsync(
            saved => saved.Id == submission.Id,
            TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task Create_Submission_Returns_Exact_Matching_Draft_Without_Creating_Another()
    {
        var existing = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "test-user-1",
            DateTime.UtcNow);
        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(existing, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var request = CreateAuthenticatedPostRequest(
            "Customer",
            SubmissionsEndpointPath,
            new
            {
                applicantName = "Jane Applicant",
                applicantEmail = "jane@example.com",
                companyName = "Example Company"
            },
            "test-user-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.GetProperty("possibleDuplicate").GetBoolean());
        Assert.True(payload.GetProperty("existingDraft").GetBoolean());
        Assert.Equal(existing.Id, payload.GetProperty("submissionId").GetGuid());
        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        Assert.Equal(1, await verifyDbContext.Submissions.CountAsync(
            submission => submission.OwnerUserId == "test-user-1",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Create_Submission_Allows_An_Explicit_Second_Legitimate_Draft()
    {
        var existing = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "test-user-1",
            DateTime.UtcNow);
        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(existing, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var request = CreateAuthenticatedPostRequest(
            "Customer",
            SubmissionsEndpointPath,
            new
            {
                applicantName = "Jane Applicant",
                applicantEmail = "jane@example.com",
                companyName = "Example Company",
                createAnotherDraft = true
            },
            "test-user-1");
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(payload.GetProperty("possibleDuplicate").GetBoolean());
        Assert.False(payload.GetProperty("existingDraft").GetBoolean());
        Assert.NotEqual(existing.Id, payload.GetProperty("submissionId").GetGuid());
        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        Assert.Equal(2, await verifyDbContext.Submissions.CountAsync(
            submission => submission.OwnerUserId == "test-user-1",
            TestContext.Current.CancellationToken));
    }



    [Fact]
    public async Task Create_Quote_Returns_Created_And_Persists_Quote_For_Owned_Submitted_Submission()
    {
        var submission = CreateSubmittedSubmission("test-user-1");

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/quotes",
            CreateBaselineQuoteRequest(),
            "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;
        var quoteId = root.GetProperty("quoteId").GetGuid();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(Guid.Empty, quoteId);
        Assert.Equal(submission.Id, root.GetProperty("submissionId").GetGuid());
        Assert.Equal("Quoted", root.GetProperty("status").GetString());
        Assert.Equal("Moderate", root.GetProperty("riskTier").GetString());
        Assert.True(root.GetProperty("premium").GetDecimal() > 0);
        Assert.Equal(1_000_000m, root.GetProperty("requestedLimit").GetDecimal());
        Assert.Equal(10_000m, root.GetProperty("retention").GetDecimal());
        var providerIndication = root.GetProperty("providerIndication");
        Assert.Equal("Contoso Specialty", providerIndication.GetProperty("providerName").GetString());
        Assert.Equal("Succeeded", providerIndication.GetProperty("status").GetString());
        Assert.Equal("Quoted", providerIndication.GetProperty("marketDisposition").GetString());
        Assert.Equal("CNT-Q-TEST-1", providerIndication.GetProperty("providerQuoteNumber").GetString());
        Assert.Equal($"/api/v1/quotes/{quoteId}", response.Headers.Location?.OriginalString);

        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedQuote = await verifyDbContext.Set<Quote>().SingleAsync(
            quote => quote.Id == quoteId,
            TestContext.Current.CancellationToken);
        var outboxMessage = await verifyDbContext.Set<OutboxMessage>().SingleAsync(
            message => message.Type == nameof(QuoteGeneratedDomainEvent),
            TestContext.Current.CancellationToken);
        var providerAttempt = await verifyDbContext.Set<QuoteRatingProviderAttempt>().SingleAsync(
            attempt => attempt.QuoteId == quoteId,
            TestContext.Current.CancellationToken);

        Assert.Equal(submission.Id, savedQuote.SubmissionId);
        Assert.Equal("test-user-1", savedQuote.OwnerUserId);
        Assert.Equal(QuoteStatus.Quoted, savedQuote.Status);
        Assert.Equal("Contoso Specialty", providerAttempt.ProviderName);
        Assert.Equal(RatingProviderAttemptStatus.Succeeded, providerAttempt.Status);
        Assert.Equal(RatingProviderMarketDisposition.Quoted, providerAttempt.MarketDisposition);
        Assert.Equal("CNT-Q-TEST-1", providerAttempt.ProviderQuoteNumber);
        Assert.NotEqual(string.Empty, providerAttempt.RequestPayloadHash);
        Assert.Contains(quoteId.ToString(), outboxMessage.Payload);
    }



    [Fact]
    public async Task Create_Quote_Returns_Existing_Quote_For_Repeated_Request()
    {
        var submission = CreateSubmittedSubmission("test-user-1");
        var requestBody = CreateBaselineQuoteRequest();

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var firstRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/quotes",
            requestBody,
            "test-user-1");
        using var firstResponse = await httpClient.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        var firstContent = await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var firstPayload = JsonDocument.Parse(firstContent);
        var firstQuoteId = firstPayload.RootElement.GetProperty("quoteId").GetGuid();

        using var secondRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/quotes",
            requestBody,
            "test-user-1");
        using var secondResponse = await httpClient.SendAsync(secondRequest, TestContext.Current.CancellationToken);
        var secondContent = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var secondPayload = JsonDocument.Parse(secondContent);
        var secondRoot = secondPayload.RootElement;

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(firstQuoteId, secondRoot.GetProperty("quoteId").GetGuid());
        Assert.Equal("AlreadyCreated", secondRoot.GetProperty("providerIndication").GetProperty("status").GetString());

        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        Assert.Equal(
            1,
            await verifyDbContext.Quotes.CountAsync(
                quote => quote.SubmissionId == submission.Id,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            1,
            await verifyDbContext.QuoteRatingProviderAttempts.CountAsync(
                attempt => attempt.Quote.SubmissionId == submission.Id,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            1,
            await verifyDbContext.OutboxMessages.CountAsync(
                message => message.Type == nameof(QuoteGeneratedDomainEvent),
                TestContext.Current.CancellationToken));
    }



    [Fact]
    public async Task Create_Quote_Returns_Conflict_For_Draft_Submission()
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "test-user-1",
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/quotes",
            CreateBaselineQuoteRequest(),
            "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }



    [Fact]
    public async Task Create_Quote_Returns_Not_Found_For_Submission_Owned_By_Different_User()
    {
        var otherUserSubmission = CreateSubmittedSubmission("test-user-2");

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(otherUserSubmission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{otherUserSubmission.Id}/quotes",
            CreateBaselineQuoteRequest(),
            "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }



    [Fact]
    public async Task Create_Quote_Returns_Referred_For_High_Risk_Profile()
    {
        var submission = CreateSubmittedSubmission("test-user-1");

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var httpRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/quotes",
            CreateHighRiskQuoteRequest(),
            "test-user-1");
        using var response = await httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var root = payload.RootElement;
        var referralReasons = root.GetProperty("referralReasons").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Referred", root.GetProperty("status").GetString());
        Assert.Equal("Severe", root.GetProperty("riskTier").GetString());
        Assert.NotEmpty(referralReasons);
    }



    [Fact]
    public async Task Create_Quote_With_Idempotency_Key_Returns_Same_Response_And_Creates_One_Quote()
    {
        var submission = CreateSubmittedSubmission("test-user-1");
        var requestBody = CreateBaselineQuoteRequest();
        var idempotencyKey = "create-quote-key-1";

        using (var scope = webApplicationFactory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
            await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var firstRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/quotes",
            requestBody,
            "test-user-1",
            idempotencyKey);
        using var firstResponse = await httpClient.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        var firstContent = await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var secondRequest = CreateAuthenticatedPostRequest(
            "Customer",
            $"{SubmissionsEndpointPath}/{submission.Id}/quotes",
            requestBody,
            "test-user-1",
            idempotencyKey);
        using var secondResponse = await httpClient.SendAsync(secondRequest, TestContext.Current.CancellationToken);
        var secondContent = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(firstContent, secondContent);
        Assert.Equal(firstResponse.Headers.Location?.OriginalString, secondResponse.Headers.Location?.OriginalString);

        using var verifyScope = webApplicationFactory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        var savedQuoteCount = await verifyDbContext.Set<Quote>().CountAsync(
            quote => quote.SubmissionId == submission.Id,
            TestContext.Current.CancellationToken);
        var quoteOutboxMessageCount = await verifyDbContext.Set<OutboxMessage>().CountAsync(
            message => message.Type == nameof(QuoteGeneratedDomainEvent),
            TestContext.Current.CancellationToken);
        var providerAttemptCount = await verifyDbContext.Set<QuoteRatingProviderAttempt>().CountAsync(
            attempt => attempt.Quote.SubmissionId == submission.Id,
            TestContext.Current.CancellationToken);
        var providerClient = webApplicationFactory.Services.GetRequiredService<TestRatingProviderClient>();

        Assert.Equal(1, savedQuoteCount);
        Assert.Equal(1, quoteOutboxMessageCount);
        Assert.Equal(1, providerAttemptCount);
        Assert.Equal(1, providerClient.CallCount);
    }



    private static Submission CreateSubmittedSubmission(string ownerUserId)
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            ownerUserId,
            new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc));
        submission.Submit();
        submission.ClearDomainEvents();

        return submission;
    }



    private static object CreateBaselineQuoteRequest()
    {
        return new
        {
            industryClass = "ProfessionalServices",
            annualRevenueBand = "From10MTo50M",
            requestedLimit = 1_000_000m,
            retention = 10_000m,
            mfaStatus = "Implemented",
            edrStatus = "Implemented",
            backupMaturity = "Mature",
            hasIncidentResponsePlan = true,
            priorCyberIncidents = 0,
            sensitiveDataExposure = "Moderate"
        };
    }



    private static object CreateHighRiskQuoteRequest()
    {
        return new
        {
            industryClass = "Healthcare",
            annualRevenueBand = "From50MTo250M",
            requestedLimit = 5_000_000m,
            retention = 2_500m,
            mfaStatus = "NotImplemented",
            edrStatus = "NotImplemented",
            backupMaturity = "Weak",
            hasIncidentResponsePlan = false,
            priorCyberIncidents = 2,
            priorCyberIncidentTypes = new[] { "Ransomware", "Data breach" },
            priorCyberIncidentDetails = "Two prior incidents affected regulated systems; recovery and control remediation are under review.",
            sensitiveDataExposure = "High"
        };
    }



    private sealed class TestRatingProviderClient : IRatingProviderClient
    {
        public int CallCount { get; private set; }

        public Task<RatingProviderResult> GetMarketIndicationAsync(
            RatingProviderRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;

            return Task.FromResult(RatingProviderResult.Succeeded(
                providerName: "Contoso Specialty",
                marketDisposition: RatingProviderMarketDisposition.Quoted,
                providerReference: "CNT-REF-TEST-1",
                providerQuoteNumber: "CNT-Q-TEST-1",
                indicatedPremium: request.LocalPremium + 500m,
                indicatedLimit: request.RequestedLimit,
                indicatedRetention: request.Retention,
                httpStatusCode: 200,
                attemptCount: 1,
                duration: TimeSpan.FromMilliseconds(25),
                completedAtUtc: new DateTime(2026, 6, 21, 1, 0, 0, DateTimeKind.Utc)));
        }
    }



    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
        databaseConnection.Dispose();
    }

}
