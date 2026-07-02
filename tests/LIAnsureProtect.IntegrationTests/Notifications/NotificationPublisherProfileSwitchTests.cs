using LIAnsureProtect.Modules.Notifications.Application;
using LIAnsureProtect.Modules.Notifications.Infrastructure;
using LIAnsureProtect.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.IntegrationTests.Notifications;

/// <summary>
/// Proves the Local ⇄ AWS deploy switch on the notification publisher port: the active
/// <see cref="PlatformProfile"/> selects the adapter (local vs SNS), and an Aws profile with no
/// topic configured fails fast rather than silently mis-wiring a topicless SNS client.
/// </summary>
public sealed class NotificationPublisherProfileSwitchTests
{
    private const string TestConnectionString =
        "Host=localhost;Database=liansureprotect_test;Username=postgres;Password=postgres";

    [Fact]
    public void LocalProfileWiresTheLocalNotificationPublisher()
    {
        var services = new ServiceCollection();
        services.AddNotificationsModule(TestConnectionString, PlatformProfile.Local);

        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<INotificationPublisher>();

        Assert.IsType<LocalNotificationPublisher>(publisher);
    }

    [Fact]
    public void AwsProfileWiresTheSnsNotificationPublisher()
    {
        var services = new ServiceCollection();
        services.Configure<NotificationPublisherOptions>(options => options.Sns = new SnsNotificationPublisherOptions
        {
            TopicArn = "arn:aws:sns:us-east-1:000000000000:liansureprotect-notifications",
            ServiceUrl = "http://localhost:4566",
            AccessKeyId = "test",
            SecretAccessKey = "test"
        });
        services.AddNotificationsModule(TestConnectionString, PlatformProfile.Aws);

        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<INotificationPublisher>();

        Assert.IsType<SnsNotificationPublisher>(publisher);
    }

    [Fact]
    public void AwsProfileFailsFastWhenTopicMissing()
    {
        var services = new ServiceCollection();
        services.AddNotificationsModule(TestConnectionString, PlatformProfile.Aws);

        using var provider = services.BuildServiceProvider();

        // No Notifications:Sns configured → resolving the publisher must fail fast.
        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<INotificationPublisher>());
    }
}
