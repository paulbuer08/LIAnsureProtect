namespace LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;

public sealed record ListQuoteReferralsResult(
    IReadOnlyCollection<QuoteReferralResult> QuoteReferrals);

public sealed record QuoteReferralResult(
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    string RiskTier,
    string Status,
    IReadOnlyCollection<string> Subjectivities,
    IReadOnlyCollection<string> ReferralReasons,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    QuoteReferralOperationsSummaryResult? Operations,
    QuoteReferralEvidenceSummaryResult Evidence);

public sealed record QuoteReferralOperationsSummaryResult(
    string? AssignedUnderwriterUserId,
    string Priority,
    DateTime DueAtUtc,
    bool IsSlaBreached,
    string Status,
    int OpenTaskCount,
    DateTime? LatestTimelineAtUtc);

public sealed record QuoteReferralEvidenceSummaryResult(
    int OpenRequestCount,
    int RespondedRequestCount,
    int OverdueRequestCount,
    DateTime? NextOpenDueAtUtc,
    bool IsWaitingForInformation,
    DateTime? LatestEvidenceActivityAtUtc);
