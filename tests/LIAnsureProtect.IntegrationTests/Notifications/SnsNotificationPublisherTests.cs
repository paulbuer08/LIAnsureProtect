using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Infrastructure;
using Microsoft.Extensions.Options;
using Moq;

namespace LIAnsureProtect.IntegrationTests.Notifications;

/// <summary>
/// Unit-level tests for the SNS notification publisher. They mock
/// <see cref="IAmazonSimpleNotificationService"/> and assert on the request we build and the result
/// we map — our logic, not the SDK's. No network, no Docker, so they run in the normal test/CI path.
/// A real SNS→SQS round trip is proven separately by the opt-in LocalStack test.
/// </summary>
public sealed class SnsNotificationPublisherTests
{
    private const string TopicArn = "arn:aws:sns:us-east-1:000000000000:liansureprotect-notifications";

    private static NotificationMessage BuildMessage() => new(
        MessageId: "6f489e91",
        OutboxMessageId: Guid.Parse("6f489e91-6a6b-4cc8-bc20-c63985f2a501"),
        Type: NotificationMessageTypes.QuoteReady,
        Audience: NotificationAudiences.CustomerOrBroker,
        OwnerUserId: "customer-1",
        SubjectReferenceType: "quote",
        SubjectReferenceId: "a6f943ad-9c87-4932-9e65-8fdd97da4079",
        OccurredAtUtc: new DateTime(2026, 7, 3, 5, 0, 0, DateTimeKind.Utc),
        Attributes: new Dictionary<string, string> { ["premium"] = "1234.00" });

    [Fact]
    public async Task PublishAsync_Publishes_Envelope_To_Configured_Topic_And_Returns_Sns_Message_Id()
    {
        PublishRequest? capturedRequest = null;
        var sns = new Mock<IAmazonSimpleNotificationService>();
        sns.Setup(client => client.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PublishResponse { MessageId = "sns-message-1" });

        var publisher = CreatePublisher(sns.Object);

        var result = await publisher.PublishAsync(BuildMessage(), TestContext.Current.CancellationToken);

        Assert.NotNull(capturedRequest);
        Assert.Equal(TopicArn, capturedRequest!.TopicArn);
        // The body is a versioned envelope carrying the routing + audit fields.
        Assert.Contains(NotificationMessageTypes.QuoteReady, capturedRequest.Message);
        Assert.Contains("customer-1", capturedRequest.Message);
        Assert.Contains("schemaVersion", capturedRequest.Message);
        // Message attributes let SNS subscribers filter without parsing the body.
        Assert.Equal(NotificationMessageTypes.QuoteReady, capturedRequest.MessageAttributes["type"].StringValue);
        Assert.Equal(NotificationAudiences.CustomerOrBroker, capturedRequest.MessageAttributes["audience"].StringValue);

        Assert.True(result.IsSuccess);
        Assert.Equal("sns-message-1", result.ProviderMessageId);
    }

    [Fact]
    public async Task PublishAsync_Returns_Transient_Failure_When_Sns_Throws()
    {
        var sns = new Mock<IAmazonSimpleNotificationService>();
        sns.Setup(client => client.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSimpleNotificationServiceException("SNS is unavailable."));

        var publisher = CreatePublisher(sns.Object);

        var result = await publisher.PublishAsync(BuildMessage(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsTransient);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));
    }

    private static SnsNotificationPublisher CreatePublisher(IAmazonSimpleNotificationService snsClient)
    {
        var options = Options.Create(new NotificationPublisherOptions
        {
            Sns = new SnsNotificationPublisherOptions { TopicArn = TopicArn }
        });

        return new SnsNotificationPublisher(snsClient, options);
    }
}
