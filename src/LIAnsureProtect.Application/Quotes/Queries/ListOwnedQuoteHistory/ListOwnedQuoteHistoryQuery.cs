using LIAnsureProtect.Platform.Abstractions.Security;
using LIAnsureProtect.Application.Submissions;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Queries.ListOwnedQuoteHistory;

public sealed record ListOwnedQuoteHistoryQuery(Guid SubmissionId)
    : IRequest<OwnedQuoteHistoryResult?>;

public sealed record OwnedQuoteHistoryResult(IReadOnlyCollection<OwnedQuoteHistoryItemResult> Quotes);

public sealed record OwnedQuoteHistoryItemResult(
    Guid QuoteId,
    Guid SubmissionId,
    int Version,
    string Status,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    string RiskTier,
    string AssuranceStatus,
    int EvidenceRequiredCount,
    int EvidenceSatisfiedCount,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    Guid? SupersedesQuoteId,
    DateTime? SupersededAtUtc);

public sealed class ListOwnedQuoteHistoryQueryHandler(
    IQuoteRepository quoteRepository,
    ISubmissionRepository submissionRepository,
    ICurrentUser currentUser)
    : IRequestHandler<ListOwnedQuoteHistoryQuery, OwnedQuoteHistoryResult?>
{
    public async Task<OwnedQuoteHistoryResult?> Handle(
        ListOwnedQuoteHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to view quote history.")
            : currentUser.UserId;
        var submission = await submissionRepository.GetDetailAsync(request.SubmissionId, ownerUserId, cancellationToken);
        if (submission is null)
            return null;
        var quotes = await quoteRepository.ListOwnedForSubmissionAsync(
            request.SubmissionId,
            ownerUserId,
            cancellationToken);

        return new OwnedQuoteHistoryResult(quotes.Select(quote => new OwnedQuoteHistoryItemResult(
            quote.Id,
            quote.SubmissionId,
            quote.Version,
            quote.Status.ToString(),
            quote.Premium,
            quote.RequestedLimit,
            quote.Retention,
            quote.RiskTier.ToString(),
            quote.AssuranceStatus.ToString(),
            quote.EvidenceRequiredCount,
            quote.EvidenceSatisfiedCount,
            quote.CreatedAtUtc,
            quote.ExpiresAtUtc,
            quote.SupersedesQuoteId,
            quote.SupersededAtUtc)).ToList());
    }
}
