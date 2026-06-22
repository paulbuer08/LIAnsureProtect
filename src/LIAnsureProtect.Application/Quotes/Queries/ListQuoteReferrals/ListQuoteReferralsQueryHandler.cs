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
        var evidenceRequests = await quoteRepository.ListEvidenceRequestsForQuotesAsync(
            quotes.Select(quote => quote.Id).ToList(),
            cancellationToken);
        var operationsByQuoteId = operations.ToDictionary(operation => operation.QuoteId);
        var evidenceRequestsByQuoteId = evidenceRequests
            .GroupBy(request => request.QuoteId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var results = quotes
            .Select(quote =>
            {
                operationsByQuoteId.TryGetValue(quote.Id, out var operation);
                evidenceRequestsByQuoteId.TryGetValue(quote.Id, out var quoteEvidenceRequests);

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
                    operation is null ? null : CreateOperationsSummary(operation),
                    CreateEvidenceSummary(quoteEvidenceRequests ?? []));
            })
            .ToList();

        return new ListQuoteReferralsResult(results);
    }

    private static QuoteReferralEvidenceSummaryResult CreateEvidenceSummary(
        IReadOnlyCollection<QuoteEvidenceRequest> evidenceRequests)
    {
        return new QuoteReferralEvidenceSummaryResult(
            evidenceRequests.Count(request => request.Status == EvidenceRequestStatus.Open),
            evidenceRequests.Count(request => request.Status == EvidenceRequestStatus.Responded),
            evidenceRequests.Any(request => request.Status is EvidenceRequestStatus.Open or EvidenceRequestStatus.Responded),
            evidenceRequests
                .OrderByDescending(request => request.UpdatedAtUtc)
                .Select(request => (DateTime?)request.UpdatedAtUtc)
                .FirstOrDefault());
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
