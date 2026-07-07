using LIAnsureProtect.Modules.Claims.Domain;
using LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.IntegrationTests.Claims;

/// <summary>
/// Proves the optimistic-concurrency half of the assignment claim at the persistence level (the
/// M44.5 pattern applied to claims): two adjusters load the same unassigned claim (both see it
/// free — the domain guard cannot help either of them), both assign to themselves, and the second
/// save must fail on the Version concurrency token instead of silently stealing the assignment.
/// </summary>
public sealed class ClaimAssignmentConcurrencyTests : IDisposable
{
    private readonly SqliteConnection connection;

    public ClaimAssignmentConcurrencyTests()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
    }

    private ClaimsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ClaimsDbContext>()
            .UseSqlite(connection)
            .Options;

        return new ClaimsDbContext(options);
    }

    private static Claim FileClaim()
    {
        var claim = Claim.File(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer-1",
            "CLM-2026-0A1B2C3D",
            ClaimIncidentType.RansomwareExtortion,
            new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 12, 9, 30, 0, DateTimeKind.Utc),
            "Ransomware encrypted the file server.",
            "POL-2026-11111111",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            1_000_000m,
            25_000m,
            new DateTime(2026, 3, 13, 10, 0, 0, DateTimeKind.Utc));
        claim.ClearDomainEvents();

        return claim;
    }

    [Fact]
    public async Task Second_Racing_Assignment_Fails_On_The_Concurrency_Token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Guid claimId;

        await using (var seedContext = CreateContext())
        {
            await seedContext.Database.EnsureCreatedAsync(cancellationToken);
            var claim = FileClaim();
            claimId = claim.Id;
            seedContext.Claims.Add(claim);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        // Both adjusters load the claim while it is still unassigned.
        await using var adjusterOneContext = CreateContext();
        await using var adjusterTwoContext = CreateContext();
        var claimSeenByOne = await adjusterOneContext.Claims
            .SingleAsync(claim => claim.Id == claimId, cancellationToken);
        var claimSeenByTwo = await adjusterTwoContext.Claims
            .SingleAsync(claim => claim.Id == claimId, cancellationToken);

        // Adjuster one wins the race.
        claimSeenByOne.AssignTo("adjuster-1", DateTime.UtcNow);
        await adjusterOneContext.SaveChangesAsync(cancellationToken);

        // Adjuster two's snapshot still looks unassigned, so the domain guard passes — the stale
        // Version in the UPDATE's WHERE clause is what must stop the overwrite.
        claimSeenByTwo.AssignTo("adjuster-2", DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => adjusterTwoContext.SaveChangesAsync(cancellationToken));

        // The first assignment survives.
        await using var verifyContext = CreateContext();
        var persisted = await verifyContext.Claims
            .SingleAsync(claim => claim.Id == claimId, cancellationToken);
        Assert.Equal("adjuster-1", persisted.AssignedAdjusterUserId);
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}
