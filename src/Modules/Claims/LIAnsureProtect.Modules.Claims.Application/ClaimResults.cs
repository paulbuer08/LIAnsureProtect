using LIAnsureProtect.Modules.Claims.Domain;

namespace LIAnsureProtect.Modules.Claims.Application;

/// <summary>Summary shape for the owner's claim list and file-claim response.</summary>
public sealed record ClaimResult(
    Guid ClaimId,
    string ClaimNumber,
    Guid PolicyId,
    string PolicyNumber,
    string IncidentType,
    DateTime IncidentAtUtc,
    DateTime DiscoveredAtUtc,
    string Status,
    DateTime FiledAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Detail shape for the owner's claim page, including the append-only timeline.</summary>
public sealed record ClaimDetailResult(
    Guid ClaimId,
    string ClaimNumber,
    Guid PolicyId,
    string PolicyNumber,
    string IncidentType,
    DateTime IncidentAtUtc,
    DateTime DiscoveredAtUtc,
    string Description,
    string Status,
    decimal PolicyLimitAtFiling,
    decimal PolicyRetentionAtFiling,
    DateTime PolicyEffectiveAtFiling,
    DateTime PolicyExpirationAtFiling,
    DateTime FiledAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyCollection<ClaimTimelineEntryResult> Timeline);

public sealed record ClaimTimelineEntryResult(
    Guid EntryId,
    string EntryType,
    string Summary,
    string CreatedByUserId,
    DateTime CreatedAtUtc);

public static class ClaimResultFactory
{
    public static ClaimResult FromClaim(Claim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        return new ClaimResult(
            claim.Id,
            claim.ClaimNumber,
            claim.PolicyId,
            claim.PolicyNumberAtFiling,
            claim.IncidentType.ToString(),
            claim.IncidentAtUtc,
            claim.DiscoveredAtUtc,
            claim.Status.ToString(),
            claim.FiledAtUtc,
            claim.UpdatedAtUtc);
    }
}
