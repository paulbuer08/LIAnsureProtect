using System.Text.Json;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using LIAnsureProtect.Modules.Notifications.Application;
using Microsoft.Extensions.Options;

namespace LIAnsureProtect.Modules.Notifications.Infrastructure;

/// <summary>
/// Publishes notifications to an Amazon SNS topic (or an SNS-compatible service such as LocalStack),
/// so the event leaves the process onto a real bus that fans out to SQS subscribers. It implements
/// the same <see cref="INotificationPublisher"/> port as the local publisher, so the outbox
/// dispatcher, retry/poison handling, and in-process projection are all unchanged — only the
/// outbound publish becomes a real network call. Selected when <c>Platform:Profile=Aws</c>.
/// </summary>
public sealed class SnsNotificationPublisher(
    IAmazonSimpleNotificationService snsClient,
    IOptions<NotificationPublisherOptions> options) : INotificationPublisher
{
    // Versioned integration-event contract: bump when the envelope shape changes so subscribers
    // (and the future analytics sink) can evolve safely.
    private const int SchemaVersion = 1;

    private readonly SnsNotificationPublisherOptions snsOptions = options.Value.Sns
        ?? throw new InvalidOperationException("Notifications:Sns configuration is required when Platform:Profile=Aws.");

    public async Task<NotificationPublishResult> PublishAsync(
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        var envelope = new NotificationIntegrationEvent(
            SchemaVersion,
            message.MessageId,
            message.OutboxMessageId,
            message.Type,
            message.Audience,
            message.OwnerUserId,
            message.SubjectReferenceType,
            message.SubjectReferenceId,
            message.OccurredAtUtc,
            message.Attributes);

        var request = new PublishRequest
        {
            TopicArn = RequireTopicArn(),
            Message = JsonSerializer.Serialize(envelope, JsonSerializerOptions.Web),
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                // Let SNS subscription filter policies route by type/audience without parsing the body.
                ["type"] = new MessageAttributeValue { DataType = "String", StringValue = message.Type },
                ["audience"] = new MessageAttributeValue { DataType = "String", StringValue = message.Audience }
            }
        };

        try
        {
            var response = await snsClient.PublishAsync(request, cancellationToken);

            return string.IsNullOrWhiteSpace(response.MessageId)
                ? NotificationPublishResult.TransientFailure("SNS publish returned no message id.")
                : NotificationPublishResult.Success(response.MessageId);
        }
        catch (AmazonServiceException exception)
        {
            // Service/network errors (throttling, unavailable, timeouts) are transient: the outbox
            // dispatcher will retry with backoff and eventually park a poison message.
            return NotificationPublishResult.TransientFailure($"SNS publish failed: {exception.Message}");
        }
    }

    private string RequireTopicArn()
    {
        return string.IsNullOrWhiteSpace(snsOptions.TopicArn)
            ? throw new InvalidOperationException("Notifications:Sns:TopicArn is required when Platform:Profile=Aws.")
            : snsOptions.TopicArn;
    }
}

/// <summary>Versioned envelope published to SNS — the stable integration-event contract.</summary>
internal sealed record NotificationIntegrationEvent(
    int SchemaVersion,
    string MessageId,
    Guid OutboxMessageId,
    string Type,
    string Audience,
    string OwnerUserId,
    string SubjectReferenceType,
    string SubjectReferenceId,
    DateTime OccurredAtUtc,
    IReadOnlyDictionary<string, string> Attributes);
