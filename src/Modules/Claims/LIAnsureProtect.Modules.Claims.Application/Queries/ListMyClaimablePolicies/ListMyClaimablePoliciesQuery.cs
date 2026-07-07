using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Claims.Application.Queries.ListMyClaimablePolicies;

/// <summary>The caller's bound policies a claim can be filed against (the wizard's picker).</summary>
public sealed record ListMyClaimablePoliciesQuery : IRequest<ListMyClaimablePoliciesResult>;

public sealed record ListMyClaimablePoliciesResult(IReadOnlyList<ClaimablePolicyResult> Policies);

public sealed record ClaimablePolicyResult(
    Guid PolicyId,
    string PolicyNumber,
    DateTime EffectiveAtUtc,
    DateTime ExpirationAtUtc,
    decimal Limit,
    decimal Retention);

public sealed class ListMyClaimablePoliciesQueryHandler(
    IClaimsPolicyContextReader policyContextReader,
    ICurrentUser currentUser)
    : IRequestHandler<ListMyClaimablePoliciesQuery, ListMyClaimablePoliciesResult>
{
    public async Task<ListMyClaimablePoliciesResult> Handle(
        ListMyClaimablePoliciesQuery request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to list claimable policies.")
            : currentUser.UserId;

        var policies = await policyContextReader.ListOwnedBoundPoliciesAsync(ownerUserId, cancellationToken);

        return new ListMyClaimablePoliciesResult(
            policies
                .Select(policy => new ClaimablePolicyResult(
                    policy.PolicyId,
                    policy.PolicyNumber,
                    policy.EffectiveAtUtc,
                    policy.ExpirationAtUtc,
                    policy.Limit,
                    policy.Retention))
                .ToArray());
    }
}
