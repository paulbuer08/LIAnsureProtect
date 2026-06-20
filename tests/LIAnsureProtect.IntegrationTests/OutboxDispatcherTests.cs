using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Infrastructure.Persistence.Outbox;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.IntegrationTests;

public sealed class OutboxDispatcherTests : IDisposable
{
    private readonly SqliteConnection databaseConnection;
    private readonly SubmissionDbContext dbContext;

    public OutboxDispatcherTests()
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
    public async Task DispatchPendingMessagesAsync_Marks_Pending_Outbox_Message_Processed()
    {
        var domainEvent = new SubmissionSubmittedDomainEvent(
            Guid.Parse("6f489e91-6a6b-4cc8-bc20-c63985f2a501"),
            "test-user-1",
            new DateTime(2026, 6, 20, 1, 0, 0, DateTimeKind.Utc));
        var outboxMessage = OutboxMessage.FromDomainEvent(
            domainEvent,
            new DateTime(2026, 6, 20, 1, 0, 5, DateTimeKind.Utc));
        await dbContext.OutboxMessages.AddAsync(outboxMessage, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dispatcher = new OutboxDispatcher(dbContext);

        var processedCount = await dispatcher.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var savedMessage = await dbContext.OutboxMessages.SingleAsync(
            message => message.Id == outboxMessage.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, processedCount);
        Assert.NotNull(savedMessage.ProcessedAtUtc);
        Assert.Null(savedMessage.Error);
    }

    public void Dispose()
    {
        dbContext.Dispose();
        databaseConnection.Dispose();
    }
}
