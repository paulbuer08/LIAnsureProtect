using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Persistence.Idempotency;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.IntegrationTests;

public sealed class IdempotencyRecordCleanupTests : IDisposable
{
    private readonly SqliteConnection databaseConnection;
    private readonly SubmissionDbContext dbContext;

    public IdempotencyRecordCleanupTests()
    {
        databaseConnection = new SqliteConnection("DataSource=:memory:");
        databaseConnection.Open();

        var dbContextOptions = new DbContextOptionsBuilder<SubmissionDbContext>()
            .UseSqlite(databaseConnection)
            .Options;

        dbContext = new SubmissionDbContext(dbContextOptions);
        dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task DeleteExpiredCompletedRecordsAsync_Deletes_Only_Completed_Records_Before_Cutoff()
    {
        var expiredCompletedRecord = IdempotencyRecord.Start(
            "expired-completed-key",
            "test-user-1",
            "Submissions.Create",
            "expired-fingerprint",
            new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc));
        expiredCompletedRecord.MarkCompleted(
            StatusCodes.Status201Created,
            """{"submissionId":"3dbe95e5-b656-4642-88b3-a1e96d8029b4","status":"Draft"}""",
            "application/json",
            "/api/v1/submissions/3dbe95e5-b656-4642-88b3-a1e96d8029b4",
            new DateTime(2026, 6, 1, 8, 0, 5, DateTimeKind.Utc));

        var recentCompletedRecord = IdempotencyRecord.Start(
            "recent-completed-key",
            "test-user-1",
            "Submissions.Create",
            "recent-fingerprint",
            new DateTime(2026, 6, 20, 8, 0, 0, DateTimeKind.Utc));
        recentCompletedRecord.MarkCompleted(
            StatusCodes.Status201Created,
            """{"submissionId":"9c1534ed-20dc-4209-bf63-381d2579f077","status":"Draft"}""",
            "application/json",
            "/api/v1/submissions/9c1534ed-20dc-4209-bf63-381d2579f077",
            new DateTime(2026, 6, 20, 8, 0, 5, DateTimeKind.Utc));

        var oldInProgressRecord = IdempotencyRecord.Start(
            "old-in-progress-key",
            "test-user-1",
            "Submissions.Submit",
            "in-progress-fingerprint",
            new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc));

        await dbContext.IdempotencyRecords.AddRangeAsync(
            [expiredCompletedRecord, recentCompletedRecord, oldInProgressRecord],
            TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var cleanup = new EfCoreIdempotencyRecordCleanup(dbContext);

        var deletedCount = await cleanup.DeleteExpiredCompletedRecordsAsync(
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
            TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var remainingKeys = await dbContext.IdempotencyRecords
            .Select(record => record.Key)
            .OrderBy(key => key)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, deletedCount);
        Assert.DoesNotContain("expired-completed-key", remainingKeys);
        Assert.Contains("recent-completed-key", remainingKeys);
        Assert.Contains("old-in-progress-key", remainingKeys);
    }

    public void Dispose()
    {
        dbContext.Dispose();
        databaseConnection.Dispose();
    }
}
