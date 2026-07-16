using MediatR;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Email;

public enum EmailDomainCapabilityStatus
{
    MailCapable = 0,
    AddressFallback = 1,
    Undeliverable = 2,
    Unverified = 3
}

public sealed record EmailDomainCapabilityResult(
    EmailDomainCapabilityStatus Status,
    string Domain,
    string? Suggestion = null,
    string? UserMessage = null,
    bool IsAuthoritative = false);

public interface IRespondentEmailDomainChecker
{
    Task<EmailDomainCapabilityResult> CheckAsync(string emailAddress, CancellationToken cancellationToken);
}

public sealed record CheckRespondentEmailDomainQuery(string EmailAddress)
    : IRequest<EmailDomainCapabilityResult>;

public sealed class CheckRespondentEmailDomainQueryHandler(IRespondentEmailDomainChecker checker)
    : IRequestHandler<CheckRespondentEmailDomainQuery, EmailDomainCapabilityResult>
{
    public Task<EmailDomainCapabilityResult> Handle(
        CheckRespondentEmailDomainQuery request,
        CancellationToken cancellationToken) => checker.CheckAsync(request.EmailAddress, cancellationToken);
}
