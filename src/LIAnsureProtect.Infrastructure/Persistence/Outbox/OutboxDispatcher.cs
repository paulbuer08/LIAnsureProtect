using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

public sealed class OutboxDispatcher(SubmissionDbContext dbContext) : IOutboxDispatcher
{
    private const int BatchSize = 20;

    public async Task<int> DispatchPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var pendingMessages = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAtUtc == null)
            .OrderBy(message => message.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
            return 0;

        var processedAtUtc = DateTime.UtcNow;

        foreach (var message in pendingMessages)
        {
            message.MarkProcessed(processedAtUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return pendingMessages.Count;
    }
}
