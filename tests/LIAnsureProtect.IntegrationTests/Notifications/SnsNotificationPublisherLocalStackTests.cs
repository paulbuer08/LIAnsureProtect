using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Infrastructure;
using Microsoft.Extensions.Options;

namespace LIAnsureProtect.IntegrationTests.Notifications;

/// <summary>
/// Opt-in round-trip test proving the SNS publisher really puts a message onto a real bus that
/// fans out to a subscribed SQS queue (with a DLQ redrive policy) — run against LocalStack (no AWS
/// account, no bill). Skipped by default (like the S3 and PostgreSQL opt-ins) so the standard
/// test/CI path stays green; enable with <c>LIANSUREPROTECT_RUN_SNS_TESTS=true</c> after starting
/// LocalStack (<c>docker compose --profile aws-local up -d</c>).
/// </summary>
public sealed class SnsNotificationPublisherLocalStackTests
{
    private const string EnabledEnvironmentVariableName = "LIANSUREPROTECT_RUN_SNS_TESTS";
    private const string ServiceUrlEnvironmentVariableName = "LIANSUREPROTECT_TEST_SNS_SERVICE_URL";
    private const string DefaultServiceUrl = "http://localhost:4566";

    [Fact]
    public async Task Publishes_To_Sns_And_Message_Arrives_In_Subscribed_Sqs_Queue()
    {
        Assert.SkipUnless(
            SnsTestsAreEnabled(),
            $"Set {EnabledEnvironmentVariableName}=true (and start LocalStack) to run SNS-backed integration tests.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var serviceUrl = GetServiceUrl();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        using var snsClient = new AmazonSimpleNotificationServiceClient(
            "test", "test", new AmazonSimpleNotificationServiceConfig { ServiceURL = serviceUrl });
        using var sqsClient = new AmazonSQSClient(
            "test", "test", new AmazonSQSConfig { ServiceURL = serviceUrl });

        // Topology: SNS topic -> main SQS queue (raw delivery), with a DLQ redrive policy so a
        // repeatedly failing message is parked instead of blocking the queue.
        var topicArn = (await snsClient.CreateTopicAsync(
            new CreateTopicRequest($"liansureprotect-notifications-{suffix}"), cancellationToken)).TopicArn;

        var deadLetterQueueUrl = (await sqsClient.CreateQueueAsync(
            new CreateQueueRequest($"liansureprotect-notifications-dlq-{suffix}"), cancellationToken)).QueueUrl;
        var deadLetterQueueArn = await GetQueueArnAsync(sqsClient, deadLetterQueueUrl, cancellationToken);

        var mainQueueUrl = (await sqsClient.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = $"liansureprotect-notifications-{suffix}",
            Attributes = new Dictionary<string, string>
            {
                ["RedrivePolicy"] =
                    $"{{\"deadLetterTargetArn\":\"{deadLetterQueueArn}\",\"maxReceiveCount\":\"3\"}}"
            }
        }, cancellationToken)).QueueUrl;
        var mainQueueArn = await GetQueueArnAsync(sqsClient, mainQueueUrl, cancellationToken);

        await snsClient.SubscribeAsync(new SubscribeRequest
        {
            TopicArn = topicArn,
            Protocol = "sqs",
            Endpoint = mainQueueArn,
            ReturnSubscriptionArn = true,
            Attributes = new Dictionary<string, string> { ["RawMessageDelivery"] = "true" }
        }, cancellationToken);

        var publisher = new SnsNotificationPublisher(
            snsClient,
            Options.Create(new NotificationPublisherOptions
            {
                Sns = new SnsNotificationPublisherOptions { TopicArn = topicArn }
            }));

        var message = new NotificationMessage(
            MessageId: suffix,
            OutboxMessageId: Guid.NewGuid(),
            Type: NotificationMessageTypes.QuoteReady,
            Audience: NotificationAudiences.CustomerOrBroker,
            OwnerUserId: "customer-roundtrip",
            SubjectReferenceType: "quote",
            SubjectReferenceId: Guid.NewGuid().ToString(),
            OccurredAtUtc: DateTime.UtcNow,
            Attributes: new Dictionary<string, string> { ["premium"] = "4321.00" });

        var publishResult = await publisher.PublishAsync(message, cancellationToken);

        Assert.True(publishResult.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(publishResult.ProviderMessageId));

        var received = await ReceiveOneAsync(sqsClient, mainQueueUrl, cancellationToken);

        Assert.NotNull(received);
        using var envelope = JsonDocument.Parse(received!.Body);
        Assert.Equal(NotificationMessageTypes.QuoteReady, envelope.RootElement.GetProperty("type").GetString());
        Assert.Equal("customer-roundtrip", envelope.RootElement.GetProperty("ownerUserId").GetString());
        Assert.Equal(1, envelope.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    private static async Task<string> GetQueueArnAsync(
        IAmazonSQS sqsClient,
        string queueUrl,
        CancellationToken cancellationToken)
    {
        var attributes = await sqsClient.GetQueueAttributesAsync(
            new GetQueueAttributesRequest { QueueUrl = queueUrl, AttributeNames = ["QueueArn"] },
            cancellationToken);

        return attributes.QueueARN;
    }

    private static async Task<Message?> ReceiveOneAsync(
        IAmazonSQS sqsClient,
        string queueUrl,
        CancellationToken cancellationToken)
    {
        // A few short long-polls to absorb SNS→SQS delivery latency.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = 3
            }, cancellationToken);

            if (response.Messages is { Count: > 0 })
                return response.Messages[0];
        }

        return null;
    }

    private static bool SnsTestsAreEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(EnabledEnvironmentVariableName),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetServiceUrl()
    {
        return Environment.GetEnvironmentVariable(ServiceUrlEnvironmentVariableName) ?? DefaultServiceUrl;
    }
}
