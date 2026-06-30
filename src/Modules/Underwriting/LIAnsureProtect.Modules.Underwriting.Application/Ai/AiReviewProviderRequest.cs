namespace LIAnsureProtect.Modules.Underwriting.Application.Ai;

public sealed record AiReviewProviderRequest(
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    string RiskTier,
    string Status,
    string StrategyName,
    IReadOnlyCollection<string> Subjectivities,
    IReadOnlyCollection<string> ReferralReasons,
    IReadOnlyCollection<string> PriorUnderwritingReviews,
    string PromptVersion,
    string OutputSchemaVersion,
    DateTime RequestedAtUtc);
