namespace LIAnsureProtect.Application.Policies.Queries;

public sealed record PolicyReadModel(
    Guid PolicyId,
    string PolicyNumber,
    string ContractualStatus,
    DateTime EffectiveDateUtc,
    DateTime ExpirationDateUtc,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    Guid QuoteId,
    Guid SubmissionId,
    string QuoteStatusAtBind,
    string QuoteRiskTierAtBind,
    string QuoteSubjectivitiesAtBind,
    string ApplicantName,
    string CompanyName);
