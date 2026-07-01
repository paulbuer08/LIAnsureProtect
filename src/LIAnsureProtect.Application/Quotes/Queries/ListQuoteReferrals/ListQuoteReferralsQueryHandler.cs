using LIAnsureProtect.Modules.Underwriting.Application.Evidence;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;

public sealed class ListQuoteReferralsQueryHandler(
    IQuoteRepository quoteRepository,
    IReferralOperationsReader referralOperationsReader,
    IEvidenceRequestsReader evidenceRequestsReader)
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
        var evidenceSummaries = await evidenceRequestsReader.GetSummariesAsync(
            quotes.Select(quote => quote.Id).ToList(),
            cancellationToken);
        var operationsByQuoteId = operationSummaries.ToDictionary(summary => summary.QuoteId);
        var evidenceSummariesByQuoteId = evidenceSummaries.ToDictionary(summary => summary.QuoteId);
        var results = quotes
            .Select(quote =>
            {
                operationsByQuoteId.TryGetValue(quote.Id, out var operationSummary);
                evidenceSummariesByQuoteId.TryGetValue(quote.Id, out var evidenceSummary);

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
                    CreateEvidenceSummary(evidenceSummary));
            })
            .ToList();

        return new ListQuoteReferralsResult(results);
    }

    private static QuoteReferralEvidenceSummaryResult CreateEvidenceSummary(
        EvidenceRequestSummaryItem? evidenceSummary)
    {
        if (evidenceSummary is null)
        {
            return new QuoteReferralEvidenceSummaryResult(
                0,
                0,
                0,
                0,
                0,
                0,
                null,
                false,
                null);
        }

        return new QuoteReferralEvidenceSummaryResult(
            evidenceSummary.OpenRequestCount,
            evidenceSummary.RespondedRequestCount,
            evidenceSummary.UnreviewedRespondedRequestCount,
            evidenceSummary.SatisfiedRequestCount,
            evidenceSummary.NeedsAttentionRequestCount,
            evidenceSummary.OverdueRequestCount,
            evidenceSummary.NextOpenDueAtUtc,
            evidenceSummary.IsWaitingForInformation,
            evidenceSummary.LatestEvidenceActivityAtUtc);
    }

    private static IReadOnlyCollection<string> SplitLines(string value)
    {
        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
