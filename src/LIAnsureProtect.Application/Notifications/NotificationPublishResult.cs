namespace LIAnsureProtect.Application.Notifications;

public sealed record NotificationPublishResult(
    bool IsSuccess,
    string? ProviderMessageId,
    string? FailureReason,
    bool IsTransient)
{
    public static NotificationPublishResult Success(string providerMessageId)
    {
        if (string.IsNullOrWhiteSpace(providerMessageId))
            throw new ArgumentException("Provider message id is required.", nameof(providerMessageId));

        return new NotificationPublishResult(true, providerMessageId.Trim(), null, false);
    }

    public static NotificationPublishResult TransientFailure(string failureReason)
    {
        return Failure(failureReason, isTransient: true);
    }

    public static NotificationPublishResult PermanentFailure(string failureReason)
    {
        return Failure(failureReason, isTransient: false);
    }

    private static NotificationPublishResult Failure(string failureReason, bool isTransient)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("Failure reason is required.", nameof(failureReason));

        return new NotificationPublishResult(false, null, failureReason.Trim(), isTransient);
    }
}
