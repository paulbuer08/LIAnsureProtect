using LIAnsureProtect.Modules.Notifications.Application;

namespace LIAnsureProtect.UnitTests.Modules.Notifications;

public sealed class NotificationInboxTitlesTests
{
    [Fact]
    public void Quote_ready_title_identifies_the_exact_version_when_available()
    {
        var title = NotificationInboxTitles.For(
            NotificationMessageTypes.QuoteReady,
            new Dictionary<string, string> { ["version"] = "3" });

        Assert.Equal("Quote version 3 is ready", title);
    }

    [Fact]
    public void Evidence_request_title_identifies_the_requested_evidence_when_available()
    {
        var title = NotificationInboxTitles.For(
            NotificationMessageTypes.EvidenceRequestCreated,
            new Dictionary<string, string>
            {
                ["requestTitle"] = "Verify privileged-account MFA"
            });

        Assert.Equal("Evidence requested: Verify privileged-account MFA", title);
    }

    [Fact]
    public void Legacy_messages_keep_clear_fallback_titles()
    {
        Assert.Equal(
            "Your quote is ready",
            NotificationInboxTitles.For(NotificationMessageTypes.QuoteReady));
        Assert.Equal(
            "Evidence requested",
            NotificationInboxTitles.For(NotificationMessageTypes.EvidenceRequestCreated));
    }
}
