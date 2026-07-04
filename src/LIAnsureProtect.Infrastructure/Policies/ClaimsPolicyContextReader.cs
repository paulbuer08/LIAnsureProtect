using LIAnsureProtect.Infrastructure.Persistence;
using LIAnsureProtect.Modules.Claims.Application;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Policies;

/// <summary>
/// Legacy-side adapter for the Claims module's <see cref="IClaimsPolicyContextReader"/> port. It
/// reads a read-only snapshot of a policy (owned by the legacy Policy context) so the Claims
/// module can validate and enrich claim filing without referencing the Policy aggregate or its
/// tables.
/// </summary>
public sealed class ClaimsPolicyContextReader(SubmissionDbContext dbContext) : IClaimsPolicyContextReader
{
    public async Task<ClaimsPolicySnapshot?> GetForClaimFilingAsync(
        Guid policyId,
        CancellationToken cancellationToken)
    {
        var policy = await dbContext.Policies
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == policyId, cancellationToken);

        if (policy is null)
            return null;

        return new ClaimsPolicySnapshot(
            policy.Id,
            policy.SubmissionId,
            policy.PolicyNumber,
            policy.OwnerUserId,
            policy.EffectiveDateUtc,
            policy.ExpirationDateUtc,
            policy.RequestedLimit,
            policy.Retention,
            policy.Status.ToString());
    }

    public async Task<IReadOnlyList<ClaimsPolicySnapshot>> ListOwnedBoundPoliciesAsync(
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Policies
            .AsNoTracking()
            .Where(policy => policy.OwnerUserId == ownerUserId
                && policy.Status == Domain.Policies.PolicyStatus.Bound)
            .OrderByDescending(policy => policy.EffectiveDateUtc)
            .Select(policy => new ClaimsPolicySnapshot(
                policy.Id,
                policy.SubmissionId,
                policy.PolicyNumber,
                policy.OwnerUserId,
                policy.EffectiveDateUtc,
                policy.ExpirationDateUtc,
                policy.RequestedLimit,
                policy.Retention,
                policy.Status.ToString()))
            .ToListAsync(cancellationToken);
    }
}
