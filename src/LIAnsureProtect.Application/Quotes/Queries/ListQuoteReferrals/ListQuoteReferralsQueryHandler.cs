using MediatR;
using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;

public sealed class ListQuoteReferralsQueryHandler(IQuoteRepository quoteRepository)
    : IRequestHandler<ListQuoteReferralsQuery, ListQuoteReferralsResult>
{
    public async Task<ListQuoteReferralsResult> Handle(
        ListQuoteReferralsQuery request,
        CancellationToken cancellationToken)
    {
        var quotes = await quoteRepository.ListPendingReferralsAsync(cancellationToken);
        var operations = await quoteRepository.ListReferralOperationsAsync(
            quotes.Select(quote => quote.Id).ToList(),
            cancellationToken);
        var operationsByQuoteId = operations.ToDictionary(operation => operation.QuoteId);
        var results = quotes
            .Select(quote =>
            {
                operationsByQuoteId.TryGetValue(quote.Id, out var operation);

                return new QuoteReferralResult(
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
                    quote.ExpiresAtUtc,
                    operation is null ? null : CreateOperationsSummary(operation));
            })
            .ToList();

        return new ListQuoteReferralsResult(results);
    }

    private static QuoteReferralOperationsSummaryResult CreateOperationsSummary(QuoteReferralOperation operation)
    {
        return new QuoteReferralOperationsSummaryResult(
            operation.AssignedUnderwriterUserId,
            operation.Priority.ToString(),
            operation.DueAtUtc,
            operation.DueAtUtc < DateTime.UtcNow && operation.Status != ReferralOperationStatus.Closed,
            operation.Status.ToString(),
            operation.Tasks.Count(task => !task.IsCompleted),
            operation.TimelineEntries
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Select(entry => (DateTime?)entry.CreatedAtUtc)
                .FirstOrDefault());
    }

    private static IReadOnlyCollection<string> SplitLines(string value)
    {
        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
