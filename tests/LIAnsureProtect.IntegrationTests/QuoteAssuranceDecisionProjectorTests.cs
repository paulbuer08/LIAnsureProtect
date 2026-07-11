using LIAnsureProtect.Application.Quotes.Assurance;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Quotes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.IntegrationTests;

public sealed class QuoteAssuranceDecisionProjectorTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly SubmissionDbContext dbContext;

    public QuoteAssuranceDecisionProjectorTests()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        dbContext = new SubmissionDbContext(
            new DbContextOptionsBuilder<SubmissionDbContext>()
                .UseSqlite(connection)
                .Options);
        dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task Human_reviews_update_matching_assertions_and_replay_does_not_over_count()
    {
        var quote = await SeedProvisionalQuoteAsync();
        var projector = new QuoteAssuranceDecisionProjector(dbContext);
        var mfaDecision = new QuoteAssuranceDecisionEvent(
            Guid.NewGuid(),
            quote.Id,
            "MultiFactorAuthentication",
            true,
            "underwriter-1",
            new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc));

        await projector.ProjectAsync(mfaDecision, TestContext.Current.CancellationToken);
        await projector.ProjectAsync(mfaDecision, TestContext.Current.CancellationToken);
        await projector.ProjectAsync(
            new QuoteAssuranceDecisionEvent(
                Guid.NewGuid(),
                quote.Id,
                "BackupRecovery",
                true,
                "underwriter-1",
                new DateTime(2026, 7, 13, 1, 0, 0, DateTimeKind.Utc)),
            TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var saved = await dbContext.Quotes
            .Include(item => item.ControlAssertions)
            .SingleAsync(item => item.Id == quote.Id, TestContext.Current.CancellationToken);

        Assert.Equal(2, saved.EvidenceSatisfiedCount);
        Assert.Equal(QuoteAssuranceStatus.Verified, saved.AssuranceStatus);
        Assert.Equal(2, await dbContext.QuoteAssuranceDecisionProjectedMessages.CountAsync(
            TestContext.Current.CancellationToken));
        Assert.All(saved.ControlAssertions, assertion =>
            Assert.Equal(ControlAssuranceState.HumanVerified, assertion.AssuranceState));
    }

    [Fact]
    public async Task Remediation_decision_keeps_quote_blocked_and_marks_assertion_rejected()
    {
        var quote = await SeedProvisionalQuoteAsync();
        var projector = new QuoteAssuranceDecisionProjector(dbContext);

        await projector.ProjectAsync(
            new QuoteAssuranceDecisionEvent(
                Guid.NewGuid(),
                quote.Id,
                "MultiFactorAuthentication",
                false,
                "underwriter-1",
                new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc)),
            TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var saved = await dbContext.Quotes
            .Include(item => item.ControlAssertions)
            .SingleAsync(item => item.Id == quote.Id, TestContext.Current.CancellationToken);

        Assert.Equal(QuoteAssuranceStatus.Rejected, saved.AssuranceStatus);
        Assert.Equal(0, saved.EvidenceSatisfiedCount);
        Assert.Equal(
            ControlAssuranceState.Rejected,
            saved.ControlAssertions.Single(item =>
                item.ControlType == ControlType.MultiFactorAuthentication).AssuranceState);
    }

    private async Task<Quote> SeedProvisionalQuoteAsync()
    {
        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "customer-1",
            new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc));
        submission.Submit();
        submission.ClearDomainEvents();
        var quote = Quote.Generate(
            submission.Id,
            "customer-1",
            12_000m,
            1_000_000m,
            10_000m,
            CyberRiskTier.Moderate,
            "BaselineCyber",
            [],
            [],
            new DateTime(2026, 7, 12, 1, 0, 0, DateTimeKind.Utc),
            evidenceRequiredCount: 2);
        quote.AddControlAssertion(ControlAssertion.Create(
            quote.Id,
            1,
            ControlType.MultiFactorAuthentication,
            "Implemented",
            true,
            "MFA evidence required.",
            quote.CreatedAtUtc));
        quote.AddControlAssertion(ControlAssertion.Create(
            quote.Id,
            1,
            ControlType.BackupRecovery,
            "Mature",
            true,
            "Backup evidence required.",
            quote.CreatedAtUtc));
        quote.ClearDomainEvents();

        dbContext.Submissions.Add(submission);
        dbContext.Quotes.Add(quote);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return quote;
    }

    public void Dispose()
    {
        dbContext.Dispose();
        connection.Dispose();
    }
}
