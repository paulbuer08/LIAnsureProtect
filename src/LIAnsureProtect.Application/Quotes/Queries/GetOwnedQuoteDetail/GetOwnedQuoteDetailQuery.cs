using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Queries.GetOwnedQuoteDetail;

public sealed record GetOwnedQuoteDetailQuery(Guid SubmissionId, Guid QuoteId)
    : IRequest<OwnedQuoteDetailResult?>;

public sealed record OwnedQuoteDetailResult(
    Guid QuoteId,
    Guid SubmissionId,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    string RiskTier,
    string Status,
    IReadOnlyList<string> Subjectivities,
    IReadOnlyList<string> ReferralReasons,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    int Version,
    Guid? SupersedesQuoteId,
    string AssuranceStatus,
    int EvidenceRequiredCount,
    int EvidenceSatisfiedCount,
    IReadOnlyList<OwnedQuoteControlAssertionResult> ControlAssertions,
    DateTime? SupersededAtUtc);

public sealed record OwnedQuoteControlAssertionResult(
    string ControlType,
    string ClaimedState,
    string AssuranceState,
    bool EvidenceRequired,
    string EvidenceReason,
    string DetailsJson);

public sealed class GetOwnedQuoteDetailQueryHandler(
    IQuoteRepository quoteRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetOwnedQuoteDetailQuery, OwnedQuoteDetailResult?>
{
    public async Task<OwnedQuoteDetailResult?> Handle(
        GetOwnedQuoteDetailQuery request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to view a quote.")
            : currentUser.UserId;
        var quote = await quoteRepository.GetOwnedForReadAsync(
            request.SubmissionId,
            request.QuoteId,
            ownerUserId,
            cancellationToken);

        return quote is null
            ? null
            : new OwnedQuoteDetailResult(
                quote.Id,
                quote.SubmissionId,
                quote.Premium,
                quote.RequestedLimit,
                quote.Retention,
                quote.RiskTier.ToString(),
                quote.Status.ToString(),
                SplitLines(quote.Subjectivities),
                SplitLines(quote.ReferralReasons),
                quote.CreatedAtUtc,
                quote.ExpiresAtUtc,
                quote.Version,
                quote.SupersedesQuoteId,
                quote.AssuranceStatus.ToString(),
                quote.EvidenceRequiredCount,
                quote.EvidenceSatisfiedCount,
                quote.ControlAssertions.OrderBy(item => item.ControlType).Select(item =>
                    new OwnedQuoteControlAssertionResult(
                        item.ControlType.ToString(),
                        item.ClaimedState,
                        item.AssuranceState.ToString(),
                        item.EvidenceRequired,
                        item.EvidenceReason,
                        item.DetailsJson)).ToList(),
                quote.SupersededAtUtc);
    }

    private static string[] SplitLines(string value) =>
        value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
