using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes.Commands.UnderwriteQuoteReferral;

internal static class UnderwriteQuoteReferralResultFactory
{
    public static UnderwriteQuoteReferralResult FromQuote(Quote quote)
    {
        return new UnderwriteQuoteReferralResult(
            quote.Id,
            quote.SubmissionId,
            quote.Status.ToString(),
            quote.Premium,
            quote.RequestedLimit,
            quote.Retention,
            quote.ReviewedByUserId ?? string.Empty,
            quote.ReviewedAtUtc ?? throw new InvalidOperationException("Reviewed quote must have a review timestamp."),
            quote.UnderwritingDecisionReason ?? string.Empty,
            quote.UnderwritingDecisionNotes);
    }
}
