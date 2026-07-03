namespace LIAnsureProtect.Modules.Claims.Application;

/// <summary>
/// Cross-context read port: the Claims module reads a read-only snapshot of a policy (owned by
/// the legacy Policy context) to validate and enrich claim filing. Implemented on the legacy
/// side. The module never mutates a policy — it references it by id only.
/// </summary>
public interface IClaimsPolicyContextReader
{
    Task<ClaimsPolicySnapshot?> GetForClaimFilingAsync(Guid policyId, CancellationToken cancellationToken);
}

/// <summary>Read-only policy facts for filing a claim. Status is a string (cross-context).</summary>
public sealed record ClaimsPolicySnapshot(
    Guid PolicyId,
    Guid SubmissionId,
    string PolicyNumber,
    string OwnerUserId,
    DateTime EffectiveAtUtc,
    DateTime ExpirationAtUtc,
    decimal Limit,
    decimal Retention,
    string Status);
