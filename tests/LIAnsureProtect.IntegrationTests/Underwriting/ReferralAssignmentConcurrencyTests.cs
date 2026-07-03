using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.IntegrationTests.Underwriting;

/// <summary>
/// Proves the optimistic-concurrency half of Referral Queue Hardening at the persistence level:
/// two underwriters load the same unassigned operation (both see it free — the domain guard cannot
/// help either of them), both assign to themselves, and the second save must fail on the Version
/// concurrency token instead of silently stealing the assignment.
/// </summary>
public sealed class ReferralAssignmentConcurrencyTests : IDisposable
{
    private readonly SqliteConnection connection;

    public ReferralAssignmentConcurrencyTests()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
    }

    private UnderwritingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UnderwritingDbContext>()
            .UseSqlite(connection)
            .Options;

        return new UnderwritingDbContext(options);
    }

    [Fact]
    public async Task Second_Racing_Assignment_Fails_On_The_Concurrency_Token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var quoteId = Guid.NewGuid();
        var referredAtUtc = DateTime.UtcNow;

        await using (var seedContext = CreateContext())
        {
            await seedContext.Database.EnsureCreatedAsync(cancellationToken);
            seedContext.QuoteReferralOperations.Add(QuoteReferralOperation.CreateDefault(
                quoteId, "High", referredAtUtc, referredAtUtc.AddDays(30)));
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        // Both underwriters load the operation while it is still unassigned.
        await using var underwriterOneContext = CreateContext();
        await using var underwriterTwoContext = CreateContext();
        var operationSeenByOne = await underwriterOneContext.QuoteReferralOperations
            .SingleAsync(operation => operation.QuoteId == quoteId, cancellationToken);
        var operationSeenByTwo = await underwriterTwoContext.QuoteReferralOperations
            .SingleAsync(operation => operation.QuoteId == quoteId, cancellationToken);

        // Underwriter one wins the race.
        operationSeenByOne.AssignTo("underwriter-1", DateTime.UtcNow);
        await underwriterOneContext.SaveChangesAsync(cancellationToken);

        // Underwriter two's snapshot still looks unassigned, so the domain guard passes —
        // the stale Version in the UPDATE's WHERE clause is what must stop the overwrite.
        operationSeenByTwo.AssignTo("underwriter-2", DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => underwriterTwoContext.SaveChangesAsync(cancellationToken));

        // The first assignment survives.
        await using var verifyContext = CreateContext();
        var persisted = await verifyContext.QuoteReferralOperations
            .SingleAsync(operation => operation.QuoteId == quoteId, cancellationToken);
        Assert.Equal("underwriter-1", persisted.AssignedUnderwriterUserId);
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}
