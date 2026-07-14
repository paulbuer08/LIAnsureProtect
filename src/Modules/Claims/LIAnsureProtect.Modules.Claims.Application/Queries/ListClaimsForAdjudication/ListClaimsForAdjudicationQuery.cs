using MediatR;

namespace LIAnsureProtect.Modules.Claims.Application.Queries.ListClaimsForAdjudication;

/// <summary>The adjuster's queue: every open claim, newest filed first.</summary>
public sealed record ListClaimsForAdjudicationQuery(
    string? Search = null,
    string? Status = null,
    string? IncidentType = null,
    string? Assignment = null,
    bool? HasOpenInformationRequests = null) : IRequest<ListClaimsForAdjudicationResult>;

public sealed record ListClaimsForAdjudicationResult(IReadOnlyList<ClaimAdjudicationResult> Claims);

public sealed class ListClaimsForAdjudicationQueryHandler(IClaimsAdjudicationReader reader)
    : IRequestHandler<ListClaimsForAdjudicationQuery, ListClaimsForAdjudicationResult>
{
    public async Task<ListClaimsForAdjudicationResult> Handle(
        ListClaimsForAdjudicationQuery request,
        CancellationToken cancellationToken)
    {
        var claims = await reader.ListQueueAsync(cancellationToken);
        var filteredClaims = ApplyFilters(claims, request);

        return new ListClaimsForAdjudicationResult(filteredClaims);
    }

    private static List<ClaimAdjudicationResult> ApplyFilters(
        IReadOnlyList<ClaimAdjudicationResult> claims,
        ListClaimsForAdjudicationQuery request)
    {
        IEnumerable<ClaimAdjudicationResult> result = claims;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            if (search.Length > 200)
                throw new ArgumentException("Search text must not exceed 200 characters.", nameof(request));

            result = result.Where(claim =>
                claim.ClaimNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                || claim.ClaimId.ToString().Equals(search, StringComparison.OrdinalIgnoreCase)
                || claim.PolicyNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                || claim.PolicyId.ToString().Equals(search, StringComparison.OrdinalIgnoreCase)
                || (claim.AssignedAdjusterUserId?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
            result = result.Where(claim => claim.Status.Equals(request.Status.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.IncidentType))
            result = result.Where(claim => claim.IncidentType.Equals(request.IncidentType.Trim(), StringComparison.OrdinalIgnoreCase));

        result = request.Assignment?.Trim().ToLowerInvariant() switch
        {
            "assigned" => result.Where(claim => claim.AssignedAdjusterUserId is not null),
            "unassigned" => result.Where(claim => claim.AssignedAdjusterUserId is null),
            _ => result
        };

        if (request.HasOpenInformationRequests is not null)
        {
            result = request.HasOpenInformationRequests.Value
                ? result.Where(claim => claim.OpenInformationRequestCount > 0)
                : result.Where(claim => claim.OpenInformationRequestCount == 0);
        }

        return result.ToList();
    }
}
