namespace LIAnsureProtect.Application.Policies.Queries;

public sealed record PolicyResult(
    Guid PolicyId,
    string PolicyNumber,
    string ContractualStatus,
    string CoverageState,
    DateTime EffectiveDateUtc,
    DateTime ExpirationDateUtc,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    Guid QuoteId,
    Guid SubmissionId,
    string QuoteStatusAtBind,
    string QuoteRiskTierAtBind,
    IReadOnlyList<string> QuoteSubjectivitiesAtBind,
    string ApplicantName,
    string CompanyName);

public sealed record ListPoliciesResult(IReadOnlyList<PolicyResult> Policies);
