namespace LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;

public sealed record SubmissionDetailResult(
    Guid SubmissionId,
    string ApplicantName,
    string ApplicantEmail,
    string CompanyName,
    string Status,
    DateTime CreatedAtUtc,
    SubmissionQuoteSummaryResult? LatestQuote = null,
    SubmissionPolicySummaryResult? RelatedPolicy = null);

public sealed record SubmissionQuoteSummaryResult(
    Guid QuoteId,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    string RiskTier,
    string Status,
    IReadOnlyList<string> Subjectivities,
    IReadOnlyList<string> ReferralReasons,
    DateTime ExpiresAtUtc,
    int Version,
    Guid? SupersedesQuoteId,
    string AssuranceStatus,
    int EvidenceRequiredCount,
    int EvidenceSatisfiedCount);

public sealed record SubmissionPolicySummaryResult(
    Guid PolicyId,
    string PolicyNumber,
    string ContractualStatus,
    string CoverageState,
    DateTime EffectiveDateUtc,
    DateTime ExpirationDateUtc);
