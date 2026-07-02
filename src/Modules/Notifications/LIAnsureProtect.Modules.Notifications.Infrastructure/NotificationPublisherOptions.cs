namespace LIAnsureProtect.Modules.Notifications.Infrastructure;

/// <summary>
/// Configuration for the outbound notification publisher. Bound from the <c>Notifications</c>
/// configuration section. The <see cref="Sns"/> section is used under <c>Platform:Profile=Aws</c>.
/// </summary>
public sealed class NotificationPublisherOptions
{
    public SnsNotificationPublisherOptions? Sns { get; set; }
}

public sealed class SnsNotificationPublisherOptions
{
    /// <summary>The SNS topic notifications are published to. Required under the Aws profile.</summary>
    public string? TopicArn { get; set; }

    /// <summary>
    /// Custom endpoint for a non-AWS SNS-compatible service (e.g. LocalStack at
    /// <c>http://localhost:4566</c>). Leave empty to target real AWS via <see cref="Region"/>.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>AWS region (e.g. <c>us-east-1</c>) used when <see cref="ServiceUrl"/> is not set.</summary>
    public string? Region { get; set; }

    /// <summary>
    /// Static access key for LocalStack only. Leave empty in real AWS so the default credential
    /// chain (instance/task role) is used — no static keys in the cloud.
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>Static secret key paired with <see cref="AccessKeyId"/> for LocalStack only.</summary>
    public string? SecretAccessKey { get; set; }
}
