using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Email;

namespace LIAnsureProtect.IntegrationTests.Security;

internal sealed class TestRespondentEmailVerificationSender : IRespondentEmailVerificationSender
{
    public RespondentEmailVerificationMessage? LastMessage { get; private set; }

    public Task<bool> SendAsync(
        RespondentEmailVerificationMessage message,
        CancellationToken cancellationToken)
    {
        LastMessage = message;
        return Task.FromResult(true);
    }
}
