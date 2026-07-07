using LIAnsureProtect.Platform.Abstractions.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;

/// <summary>The Claims module outbox exposed as an <see cref="IOutboxSource"/>.</summary>
public sealed class ClaimsOutboxSource(ClaimsDbContext dbContext) : IOutboxSource
{
    public async Task<IReadOnlyList<IOutboxMessageView>> GetPendingAsync(
        int batchSize,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var pending = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAtUtc == null
                && message.FailedAtUtc == null
                && (message.NextAttemptAtUtc == null || message.NextAttemptAtUtc <= nowUtc))
            .OrderBy(message => message.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return pending;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
