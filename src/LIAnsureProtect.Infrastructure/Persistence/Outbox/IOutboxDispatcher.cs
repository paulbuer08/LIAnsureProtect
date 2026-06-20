namespace LIAnsureProtect.Infrastructure.Persistence.Outbox;

public interface IOutboxDispatcher
{
    Task<int> DispatchPendingMessagesAsync(CancellationToken cancellationToken);
}
