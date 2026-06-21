namespace LIAnsureProtect.Application.Quotes.Commands.UnderwriteQuoteReferral;

public sealed record UnderwriteQuoteReferralResult(
    Guid QuoteId,
    Guid SubmissionId,
    string Status,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    string ReviewedByUserId,
    DateTime ReviewedAtUtc,
    string UnderwritingDecisionReason,
    string? UnderwritingDecisionNotes);
