using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace LIAnsureProtect.IntegrationTests;

public sealed class SubmissionEndpointTests(WebApplicationFactory<Program> webApplicationFactory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string SubmissionsEndpointPath = "/api/v1/submissions";
    private static readonly Uri TestServerBaseAddress = new("https://localhost");

    private readonly HttpClient httpClient = webApplicationFactory
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            });
        })
        .CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = TestServerBaseAddress,
            AllowAutoRedirect = false
        });

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
}
