namespace LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;

public sealed record SubmissionDetailResult(
    Guid SubmissionId,
    string ApplicantName,
    string ApplicantEmail,
    string CompanyName,
    string Status,
    DateTime CreatedAtUtc,
    SubmissionQuoteSummaryResult? LatestQuote = null);

public sealed record SubmissionQuoteSummaryResult(
    Guid QuoteId,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    string RiskTier,
    string Status,
    IReadOnlyList<string> Subjectivities,
    IReadOnlyList<string> ReferralReasons,
    DateTime ExpiresAtUtc);
