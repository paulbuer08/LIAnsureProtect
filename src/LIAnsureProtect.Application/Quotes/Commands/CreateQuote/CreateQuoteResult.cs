namespace LIAnsureProtect.Application.Quotes.Commands.CreateQuote;

public sealed record CreateQuoteResult(
    Guid QuoteId,
    Guid SubmissionId,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    string RiskTier,
    string Status,
    IReadOnlyList<string> Subjectivities,
    IReadOnlyList<string> ReferralReasons,
    DateTime ExpiresAtUtc,
    RatingProviderIndicationResult ProviderIndication,
    int Version,
    Guid? SupersedesQuoteId,
    string AssuranceStatus,
    int EvidenceRequiredCount,
    int EvidenceSatisfiedCount,
    IReadOnlyList<ControlAssertionResult> ControlAssertions);

public sealed record ControlAssertionResult(
    string ControlType,
    string ClaimedState,
    string AssuranceState,
    bool EvidenceRequired,
    string EvidenceReason,
    string DetailsJson);
