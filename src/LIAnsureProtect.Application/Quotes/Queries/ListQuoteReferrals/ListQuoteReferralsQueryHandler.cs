using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using MediatR;
using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;

public sealed class ListQuoteReferralsQueryHandler(
    IQuoteRepository quoteRepository,
    IReferralOperationsReader referralOperationsReader)
    : IRequestHandler<ListQuoteReferralsQuery, ListQuoteReferralsResult>
{
    public async Task<ListQuoteReferralsResult> Handle(
        ListQuoteReferralsQuery request,
        CancellationToken cancellationToken)
    {
        var quotes = await quoteRepository.ListPendingReferralsAsync(cancellationToken);
        var operationSummaries = await referralOperationsReader.GetSummariesAsync(
            quotes.Select(quote => quote.Id).ToList(),
            cancellationToken);
        var evidenceRequests = await quoteRepository.ListEvidenceRequestsForQuotesAsync(
            quotes.Select(quote => quote.Id).ToList(),
            cancellationToken);
        var operationsByQuoteId = operationSummaries.ToDictionary(summary => summary.QuoteId);
        var evidenceRequestsByQuoteId = evidenceRequests
            .GroupBy(request => request.QuoteId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var results = quotes
            .Select(quote =>
            {
                operationsByQuoteId.TryGetValue(quote.Id, out var operationSummary);
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
                    operationSummary is null ? null : new QuoteReferralOperationsSummaryResult(
                        operationSummary.AssignedUnderwriterUserId,
                        operationSummary.Priority,
                        operationSummary.DueAtUtc,
                        operationSummary.IsSlaBreached,
                        operationSummary.Status,
                        operationSummary.OpenTaskCount,
                        operationSummary.LatestTimelineAtUtc),
                    CreateEvidenceSummary(quoteEvidenceRequests ?? []));
            })
            .ToList();

        return new ListQuoteReferralsResult(results);
    }

    private static QuoteReferralEvidenceSummaryResult CreateEvidenceSummary(
        IReadOnlyCollection<QuoteEvidenceRequest> evidenceRequests)
    {
        var openRequests = evidenceRequests
            .Where(request => request.Status == EvidenceRequestStatus.Open)
            .ToList();

        return new QuoteReferralEvidenceSummaryResult(
            openRequests.Count,
            evidenceRequests.Count(request => request.Status == EvidenceRequestStatus.Responded),
            evidenceRequests.Count(request =>
                request.Status == EvidenceRequestStatus.Responded
                && request.ReviewDecision == EvidenceReviewDecisionStatus.NotReviewed),
            evidenceRequests.Count(request => request.ReviewDecision == EvidenceReviewDecisionStatus.Satisfied),
            evidenceRequests.Count(request =>
                request.ReviewDecision is EvidenceReviewDecisionStatus.Insufficient
                    or EvidenceReviewDecisionStatus.NeedsClarification),
            openRequests.Count(request => request.DueAtUtc < DateTime.UtcNow),
            openRequests
                .OrderBy(request => request.DueAtUtc)
                .Select(request => (DateTime?)request.DueAtUtc)
                .FirstOrDefault(),
            evidenceRequests.Any(request => request.Status is EvidenceRequestStatus.Open or EvidenceRequestStatus.Responded),
            evidenceRequests
                .OrderByDescending(request => request.UpdatedAtUtc)
                .Select(request => (DateTime?)request.UpdatedAtUtc)
                .FirstOrDefault());
    }

    private static IReadOnlyCollection<string> SplitLines(string value)
    {
        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
