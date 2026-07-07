namespace LIAnsureProtect.Modules.Claims.Application;

/// <summary>
/// Read-side port for the adjuster's workload (role-scoped, not owner-scoped): the queue of open
/// claims and the full working detail of one claim.
/// </summary>
public interface IClaimsAdjudicationReader
{
    /// <summary>All non-closed claims, newest filed first.</summary>
    Task<IReadOnlyList<ClaimAdjudicationResult>> ListQueueAsync(CancellationToken cancellationToken);

    Task<ClaimAdjudicationDetailResult?> GetDetailAsync(Guid claimId, CancellationToken cancellationToken);
}
