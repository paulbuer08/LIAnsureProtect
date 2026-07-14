using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Claims.Application.Queries.ListMyClaims;

/// <summary>Owner-scoped claim list: callers only ever see their own claims.</summary>
public sealed record ListMyClaimsQuery(
    string? Search = null,
    string? Status = null,
    string? IncidentType = null) : IRequest<ListMyClaimsResult>;

public sealed record ListMyClaimsResult(IReadOnlyList<ClaimResult> Claims);

public sealed class ListMyClaimsQueryHandler(
    IClaimsReader claimsReader,
    ICurrentUser currentUser)
    : IRequestHandler<ListMyClaimsQuery, ListMyClaimsResult>
{
    public async Task<ListMyClaimsResult> Handle(ListMyClaimsQuery request, CancellationToken cancellationToken)
    {
        var ownerUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to list claims.")
            : currentUser.UserId;

        var claims = await claimsReader.ListOwnerClaimsAsync(ownerUserId, cancellationToken);
        var filteredClaims = ApplyFilters(claims, request);

        return new ListMyClaimsResult(filteredClaims);
    }

    private static List<ClaimResult> ApplyFilters(
        IReadOnlyList<ClaimResult> claims,
        ListMyClaimsQuery request)
    {
        IEnumerable<ClaimResult> result = claims;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = ValidateSearch(request.Search);
            result = result.Where(claim =>
                claim.ClaimNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                || claim.ClaimId.ToString().Equals(search, StringComparison.OrdinalIgnoreCase)
                || claim.PolicyNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                || claim.PolicyId.ToString().Equals(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
            result = result.Where(claim => claim.Status.Equals(request.Status.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.IncidentType))
            result = result.Where(claim => claim.IncidentType.Equals(request.IncidentType.Trim(), StringComparison.OrdinalIgnoreCase));

        return result.ToList();
    }

    private static string ValidateSearch(string search)
    {
        var trimmed = search.Trim();
        return trimmed.Length <= 200
            ? trimmed
            : throw new ArgumentException("Search text must not exceed 200 characters.", nameof(search));
    }
}
