namespace LIAnsureProtect.Platform.Abstractions.Outbox;

public enum OutboxMessageConsumerStatus
{
    NotHandled = 0,
    Succeeded = 1,
    TransientFailure = 2,
    PermanentFailure = 3
}

public sealed record OutboxMessageConsumerResult(
    OutboxMessageConsumerStatus Status,
    string? FailureReason = null,
    string? ProviderMessageId = null)
{
    public static OutboxMessageConsumerResult NotHandled()
        => new(OutboxMessageConsumerStatus.NotHandled);

    public static OutboxMessageConsumerResult Succeeded(string? providerMessageId = null)
        => new(OutboxMessageConsumerStatus.Succeeded, ProviderMessageId: providerMessageId);

    public static OutboxMessageConsumerResult TransientFailure(string failureReason)
        => new(OutboxMessageConsumerStatus.TransientFailure, FailureReason: RequireFailureReason(failureReason));

    public static OutboxMessageConsumerResult PermanentFailure(string failureReason)
        => new(OutboxMessageConsumerStatus.PermanentFailure, FailureReason: RequireFailureReason(failureReason));

    private static string RequireFailureReason(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("Failure reason is required.", nameof(failureReason));

        return failureReason.Trim();
    }
}
