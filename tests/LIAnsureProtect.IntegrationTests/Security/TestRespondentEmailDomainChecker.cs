using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Email;

namespace LIAnsureProtect.IntegrationTests.Security;

internal sealed class TestRespondentEmailDomainChecker : IRespondentEmailDomainChecker
{
    public Task<EmailDomainCapabilityResult> CheckAsync(
        string emailAddress,
        CancellationToken cancellationToken)
    {
        var domain = emailAddress.Split('@').LastOrDefault() ?? "example.com";
        if (string.Equals(domain, "undeliverable.test", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new EmailDomainCapabilityResult(
                EmailDomainCapabilityStatus.Undeliverable,
                domain,
                UserMessage: "This email domain declares that it cannot receive email.",
                IsAuthoritative: true));
        }
        return Task.FromResult(new EmailDomainCapabilityResult(
            EmailDomainCapabilityStatus.MailCapable,
            domain,
            IsAuthoritative: true));
    }
}
