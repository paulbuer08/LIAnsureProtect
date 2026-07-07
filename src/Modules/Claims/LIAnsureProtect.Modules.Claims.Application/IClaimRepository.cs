using LIAnsureProtect.Modules.Claims.Domain;

namespace LIAnsureProtect.Modules.Claims.Application;

/// <summary>Write-side persistence port for the <see cref="Claim"/> aggregate.</summary>
public interface IClaimRepository
{
    Task AddAsync(Claim claim, CancellationToken cancellationToken);

    /// <summary>Loads a claim (with timeline) tracked for update, or null if it does not exist.</summary>
    Task<Claim?> GetByIdForUpdateAsync(Guid claimId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
