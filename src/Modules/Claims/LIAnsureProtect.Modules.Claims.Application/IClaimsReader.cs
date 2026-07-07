namespace LIAnsureProtect.Modules.Claims.Application;

/// <summary>Read-side port for owner-scoped claim reads (no-tracking projections).</summary>
public interface IClaimsReader
{
    Task<IReadOnlyList<ClaimResult>> ListOwnerClaimsAsync(
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<ClaimDetailResult?> GetOwnerClaimDetailAsync(
        string ownerUserId,
        Guid claimId,
        CancellationToken cancellationToken);
}
