using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LIAnsureProtect.Infrastructure.Persistence;
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

public sealed class SubmissionEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
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
            });
        });

        using var scope = this.webApplicationFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();
        dbContext.Database.EnsureCreated();

        httpClient = this.webApplicationFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = TestServerBaseAddress,
            AllowAutoRedirect = false
        });
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

        using var response = await httpClient.PostAsJsonAsync(
            SubmissionsEndpointPath,
            request,
            TestContext.Current.CancellationToken);
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
            TestContext.Current.CancellationToken);

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

        using var response = await httpClient.PostAsJsonAsync(
            SubmissionsEndpointPath,
            request,
            TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(content);
        var errors = payload.RootElement.GetProperty("errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(errors.TryGetProperty("ApplicantName", out _));
        Assert.True(errors.TryGetProperty("ApplicantEmail", out _));
        Assert.True(errors.TryGetProperty("CompanyName", out _));
    }

    public void Dispose()
    {
        httpClient.Dispose();
        webApplicationFactory.Dispose();
        databaseConnection.Dispose();
    }
}
