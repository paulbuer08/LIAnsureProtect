using MediatR;

namespace LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;

/// <summary>
/// Applies role-workbench filters to the complete shared referral queue. The underlying unfiltered
/// read remains the only cached value, so filter variants cannot become stale independently.
/// </summary>
public sealed record SearchQuoteReferralsQuery(
    string? Search = null,
    string? RiskTier = null,
    string? Priority = null,
    string? Assignment = null,
    string? EvidenceState = null) : IRequest<ListQuoteReferralsResult>;

public sealed class SearchQuoteReferralsQueryHandler(ISender sender)
    : IRequestHandler<SearchQuoteReferralsQuery, ListQuoteReferralsResult>
{
    public async Task<ListQuoteReferralsResult> Handle(
        SearchQuoteReferralsQuery request,
        CancellationToken cancellationToken)
    {
        var source = await sender.Send(new ListQuoteReferralsQuery(), cancellationToken);
        IEnumerable<QuoteReferralResult> referrals = source.QuoteReferrals;

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            if (search.Length > 200)
                throw new ArgumentException("Search text must not exceed 200 characters.", nameof(request));

            referrals = referrals.Where(referral =>
                referral.QuoteId.ToString().Equals(search, StringComparison.OrdinalIgnoreCase)
                || referral.SubmissionId.ToString().Equals(search, StringComparison.OrdinalIgnoreCase)
                || referral.OwnerUserId.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.RiskTier))
            referrals = referrals.Where(referral => referral.RiskTier.Equals(request.RiskTier.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.Priority))
            referrals = referrals.Where(referral => referral.Operations?.Priority.Equals(request.Priority.Trim(), StringComparison.OrdinalIgnoreCase) == true);

        referrals = request.Assignment?.Trim().ToLowerInvariant() switch
        {
            "assigned" => referrals.Where(referral => referral.Operations?.AssignedUnderwriterUserId is not null),
            "unassigned" => referrals.Where(referral => referral.Operations?.AssignedUnderwriterUserId is null),
            _ => referrals
        };

        referrals = request.EvidenceState?.Trim().ToLowerInvariant() switch
        {
            "waiting" => referrals.Where(referral => referral.Evidence.IsWaitingForInformation),
            "attention" => referrals.Where(referral => referral.Evidence.NeedsAttentionRequestCount > 0),
            "overdue" => referrals.Where(referral => referral.Evidence.OverdueRequestCount > 0),
            "satisfied" => referrals.Where(referral => referral.Evidence.OpenRequestCount == 0 && referral.Evidence.NeedsAttentionRequestCount == 0),
            _ => referrals
        };

        return new ListQuoteReferralsResult(referrals.ToList());
    }
}
