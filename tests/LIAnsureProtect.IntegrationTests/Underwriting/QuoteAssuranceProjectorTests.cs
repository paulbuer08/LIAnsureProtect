using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Modules.Underwriting.Application.Assurance;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LIAnsureProtect.IntegrationTests.Underwriting;

public sealed class QuoteAssuranceProjectorTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly UnderwritingDbContext dbContext;
    private readonly Mock<IUnderwritingQuoteContextReader> quoteContextReader;
    private readonly Guid quoteId = Guid.NewGuid();
    private readonly Guid submissionId = Guid.NewGuid();

    public QuoteAssuranceProjectorTests()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        dbContext = new UnderwritingDbContext(
            new DbContextOptionsBuilder<UnderwritingDbContext>()
                .UseSqlite(connection)
                .Options);
        dbContext.Database.EnsureCreated();

        quoteContextReader = new Mock<IUnderwritingQuoteContextReader>();
        quoteContextReader
            .Setup(reader => reader.GetForAssuranceAsync(quoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuoteAssuranceRequirementContext(
                quoteId,
                submissionId,
                "customer-1",
                1,
                null,
                [
                    new QuoteAssuranceRequirement(
                        "MultiFactorAuthentication",
                        true,
                        "MFA receives material rating credit."),
                    new QuoteAssuranceRequirement(
                        "BackupRecovery",
                        true,
                        "Mature backups receive material rating credit."),
                    new QuoteAssuranceRequirement(
                        "EndpointDetectionAndResponse",
                        false,
                        string.Empty)
                ]));
    }

    [Fact]
    public async Task Quote_generated_creates_only_required_requests_and_is_idempotent()
    {
        var sourceMessageId = Guid.NewGuid();
        var assuranceEvent = new QuoteAssuranceEvent(
            sourceMessageId,
            quoteId,
            submissionId,
            1,
            null,
            new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc));
        var projector = new QuoteAssuranceProjector(
            dbContext,
            quoteContextReader.Object,
            new EfEvidenceRequestRepository(dbContext));

        await projector.ProjectAsync(assuranceEvent, TestContext.Current.CancellationToken);
        await projector.ProjectAsync(assuranceEvent, TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var requests = await dbContext.QuoteEvidenceRequests
            .OrderBy(request => request.Category)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.Equal("system-assurance-policy", request.RequestedByUserId));
        Assert.Contains(requests, request => request.Category == EvidenceRequestCategory.MultiFactorAuthentication);
        Assert.Contains(requests, request => request.Category == EvidenceRequestCategory.BackupRecovery);
        Assert.DoesNotContain(requests, request => request.Category == EvidenceRequestCategory.EndpointDetectionAndResponse);
        Assert.Single(await dbContext.QuoteAssuranceProjectedMessages.ToListAsync(
            TestContext.Current.CancellationToken));

        quoteContextReader.Verify(
            reader => reader.GetForAssuranceAsync(quoteId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Reassessment_marks_prior_quote_requests_historical_and_creates_current_version()
    {
        var projector = new QuoteAssuranceProjector(
            dbContext,
            quoteContextReader.Object,
            new EfEvidenceRequestRepository(dbContext));
        await projector.ProjectAsync(
            new QuoteAssuranceEvent(
                Guid.NewGuid(),
                quoteId,
                submissionId,
                1,
                null,
                new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc)),
            TestContext.Current.CancellationToken);

        var replacementQuoteId = Guid.NewGuid();
        quoteContextReader
            .Setup(reader => reader.GetForAssuranceAsync(replacementQuoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuoteAssuranceRequirementContext(
                replacementQuoteId,
                submissionId,
                "customer-1",
                2,
                quoteId,
                [
                    new QuoteAssuranceRequirement(
                        "MultiFactorAuthentication",
                        true,
                        "Changed MFA assertion requires current evidence.")
                ],
                "SUB-2026-TEST",
                "Example Company"));

        await projector.ProjectAsync(
            new QuoteAssuranceEvent(
                Guid.NewGuid(),
                replacementQuoteId,
                submissionId,
                2,
                quoteId,
                new DateTime(2026, 7, 12, 1, 0, 0, DateTimeKind.Utc)),
            TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var requests = await dbContext.QuoteEvidenceRequests
            .OrderBy(request => request.QuoteVersion)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, requests.Count);
        Assert.All(
            requests.Where(request => request.QuoteVersion == 1),
            request =>
            {
                Assert.Equal(QuoteEvidenceDisposition.Superseded, request.QuoteDisposition);
                Assert.Equal(replacementQuoteId, request.SupersededByQuoteId);
                Assert.Equal(2, request.SupersededByQuoteVersion);
            });
        var current = Assert.Single(requests, request => request.QuoteVersion == 2);
        Assert.Equal(QuoteEvidenceDisposition.Current, current.QuoteDisposition);
    }

    public void Dispose()
    {
        dbContext.Dispose();
        connection.Dispose();
    }
}
