namespace LIAnsureProtect.Application.Policies.Binding;

public sealed record PolicyBindingProviderRequest(
    Guid PolicyId,
    string PolicyNumber,
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    DateTime EffectiveDateUtc,
    DateTime ExpirationDateUtc);
