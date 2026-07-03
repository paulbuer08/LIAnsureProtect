using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Application.Documents;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;

/// <summary>Owner-scoped no-tracking claim reads for the CQRS read side.</summary>
public sealed class ClaimsReader(ClaimsDbContext dbContext) : IClaimsReader
{
    public async Task<IReadOnlyList<ClaimResult>> ListOwnerClaimsAsync(
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Claims
            .AsNoTracking()
            .Where(claim => claim.OwnerUserId == ownerUserId)
            .OrderByDescending(claim => claim.FiledAtUtc)
            .Select(claim => new ClaimResult(
                claim.Id,
                claim.ClaimNumber,
                claim.PolicyId,
                claim.PolicyNumberAtFiling,
                claim.IncidentType.ToString(),
                claim.IncidentAtUtc,
                claim.DiscoveredAtUtc,
                claim.Status.ToString(),
                claim.FiledAtUtc,
                claim.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClaimDetailResult?> GetOwnerClaimDetailAsync(
        string ownerUserId,
        Guid claimId,
        CancellationToken cancellationToken)
    {
        var claim = await dbContext.Claims
            .AsNoTracking()
            .Include(candidate => candidate.TimelineEntries)
            .Include(candidate => candidate.InformationRequests)
            .Include(candidate => candidate.Documents)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == claimId && candidate.OwnerUserId == ownerUserId,
                cancellationToken);

        if (claim is null)
            return null;

        return new ClaimDetailResult(
            claim.Id,
            claim.ClaimNumber,
            claim.PolicyId,
            claim.PolicyNumberAtFiling,
            claim.IncidentType.ToString(),
            claim.IncidentAtUtc,
            claim.DiscoveredAtUtc,
            claim.Description,
            claim.Status.ToString(),
            claim.PolicyLimitAtFiling,
            claim.PolicyRetentionAtFiling,
            claim.PolicyEffectiveAtFiling,
            claim.PolicyExpirationAtFiling,
            claim.FiledAtUtc,
            claim.UpdatedAtUtc,
            claim.TimelineEntries
                .OrderBy(entry => entry.CreatedAtUtc)
                .Select(entry => new ClaimTimelineEntryResult(
                    entry.Id,
                    entry.EntryType.ToString(),
                    entry.Summary,
                    entry.CreatedByUserId,
                    entry.CreatedAtUtc))
                .ToArray(),
            claim.InformationRequests
                .OrderBy(request => request.RequestedAtUtc)
                .Select(ClaimAdjudicationResultFactory.FromInformationRequest)
                .ToArray(),
            claim.Documents
                .OrderBy(document => document.UploadedAtUtc)
                .Select(ClaimDocumentResultFactory.FromDocument)
                .ToArray());
    }
}
