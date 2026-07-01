namespace LIAnsureProtect.Platform.Abstractions.Outbox;

/// <summary>
/// One outbox the dispatcher drains. Each bounded context that emits events exposes its outbox as a
/// source; the dispatcher merges all sources' pending messages and processes them in CreatedAtUtc order.
/// </summary>
public interface IOutboxSource
{
    Task<IReadOnlyList<IOutboxMessageView>> GetPendingAsync(
        int batchSize,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
