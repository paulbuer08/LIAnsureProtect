using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes.Rating;

public sealed record CyberRatingResult(
    decimal Premium,
    CyberRiskTier RiskTier,
    IReadOnlyList<string> Subjectivities,
    IReadOnlyList<string> ReferralReasons,
    string StrategyName);
