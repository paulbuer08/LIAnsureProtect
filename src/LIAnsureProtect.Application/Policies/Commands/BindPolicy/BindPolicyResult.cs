namespace LIAnsureProtect.Application.Policies.Commands.BindPolicy;

public sealed record BindPolicyResult(
    Guid PolicyId,
    string PolicyNumber,
    Guid QuoteId,
    Guid SubmissionId,
    string Status,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    DateTime EffectiveDateUtc,
    DateTime ExpirationDateUtc,
    string BoundByUserId,
    DateTime BoundAtUtc,
    string BindingProviderName,
    string BindingReference);
