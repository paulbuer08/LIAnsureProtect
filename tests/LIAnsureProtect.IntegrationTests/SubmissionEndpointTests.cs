using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        string path,
        string userId = "test-user-1")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId);
        request.Headers.Add(TestAuthHandler.EmailHeader, "test-user@example.com");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);

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



    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
        databaseConnection.Dispose();
    }

}
