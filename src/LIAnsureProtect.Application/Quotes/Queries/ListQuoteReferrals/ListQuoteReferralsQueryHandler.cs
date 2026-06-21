using MediatR;

namespace LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;

public sealed class ListQuoteReferralsQueryHandler(IQuoteRepository quoteRepository)
    : IRequestHandler<ListQuoteReferralsQuery, ListQuoteReferralsResult>
{
    public async Task<ListQuoteReferralsResult> Handle(
        ListQuoteReferralsQuery request,
        CancellationToken cancellationToken)
    {
        var quotes = await quoteRepository.ListPendingReferralsAsync(cancellationToken);
        var results = quotes
            .Select(quote => new QuoteReferralResult(
                quote.Id,
                quote.SubmissionId,
                quote.OwnerUserId,
                quote.Premium,
                quote.RequestedLimit,
                quote.Retention,
                quote.RiskTier.ToString(),
                quote.Status.ToString(),
                SplitLines(quote.Subjectivities),
                SplitLines(quote.ReferralReasons),
                quote.CreatedAtUtc,
                quote.ExpiresAtUtc))
            .ToList();

        return new ListQuoteReferralsResult(results);
    }

    private static IReadOnlyCollection<string> SplitLines(string value)
    {
        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
