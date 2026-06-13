using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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



    private static HttpRequestMessage CreateAuthenticatedPostRequest(string role, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, SubmissionsEndpointPath)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add(TestAuthHandler.UserIdHeader, "test-user-1");
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



    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
        databaseConnection.Dispose();
    }

}
