using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.ManageQuoteReferralOperations;

public sealed record GetQuoteReferralTimelineQuery(Guid QuoteId)
    : IRequest<QuoteReferralTimelineResult?>;

public sealed record QuoteReferralTimelineResult(
    Guid QuoteId,
    IReadOnlyCollection<QuoteReferralTimelineEntryResult> Entries);

public sealed record QuoteReferralTimelineEntryResult(
    string EntryType,
    string Summary,
    string CreatedByUserId,
    DateTime CreatedAtUtc);

public sealed class GetQuoteReferralTimelineQueryHandler(
    IQuoteRepository quoteRepository,
    IReferralOperationsReader referralOperationsReader)
    : IRequestHandler<GetQuoteReferralTimelineQuery, QuoteReferralTimelineResult?>
{
    public async Task<QuoteReferralTimelineResult?> Handle(
        GetQuoteReferralTimelineQuery request,
        CancellationToken cancellationToken)
    {
        var operationEntries = await referralOperationsReader.GetTimelineAsync(request.QuoteId, cancellationToken);
        if (operationEntries is null)
            return null;

        var reviews = await quoteRepository.ListUnderwritingReviewsAsync(request.QuoteId, cancellationToken);
        var entries = operationEntries
            .Select(entry => new QuoteReferralTimelineEntryResult(
                entry.EntryType, entry.Summary, entry.CreatedByUserId, entry.CreatedAtUtc))
            .Concat(reviews.Select(review => new QuoteReferralTimelineEntryResult(
                "DecisionRecorded",
                $"Final underwriting decision audit row recorded: {review.Decision}.",
                review.ReviewedByUserId,
                review.CreatedAtUtc)))
            .OrderBy(entry => entry.CreatedAtUtc)
            .ToList();

        return new QuoteReferralTimelineResult(request.QuoteId, entries);
    }
}
