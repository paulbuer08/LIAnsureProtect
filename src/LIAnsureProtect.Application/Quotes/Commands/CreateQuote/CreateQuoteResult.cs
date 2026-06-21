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
    DateTime ExpiresAtUtc);
