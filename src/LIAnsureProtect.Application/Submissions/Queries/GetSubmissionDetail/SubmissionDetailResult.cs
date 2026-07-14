namespace LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;

public sealed record SubmissionDetailResult(
    Guid SubmissionId,
    string SubmissionReference,
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
    int EvidenceSatisfiedCount,
    IReadOnlyList<SubmissionControlAssertionResult> ControlAssertions);

public sealed record SubmissionControlAssertionResult(
    string ControlType,
    string ClaimedState,
    string AssuranceState,
    bool EvidenceRequired,
    string EvidenceReason,
    string DetailsJson);

public sealed record SubmissionPolicySummaryResult(
    Guid PolicyId,
    string PolicyNumber,
    string ContractualStatus,
    string CoverageState,
    DateTime EffectiveDateUtc,
    DateTime ExpirationDateUtc);
