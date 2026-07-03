using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Application.Documents;
using LIAnsureProtect.Modules.Claims.Domain;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;

/// <summary>Role-scoped no-tracking reads for the adjuster's queue and file view.</summary>
public sealed class ClaimsAdjudicationReader(ClaimsDbContext dbContext) : IClaimsAdjudicationReader
{
    public async Task<IReadOnlyList<ClaimAdjudicationResult>> ListQueueAsync(CancellationToken cancellationToken)
    {
        var claims = await dbContext.Claims
            .AsNoTracking()
            .Include(claim => claim.InformationRequests)
            .Where(claim => claim.Status != ClaimStatus.Closed)
            .OrderByDescending(claim => claim.FiledAtUtc)
            .ToListAsync(cancellationToken);

        return claims
            .Select(ClaimAdjudicationResultFactory.FromClaim)
            .ToArray();
    }

    public async Task<ClaimAdjudicationDetailResult?> GetDetailAsync(
        Guid claimId,
        CancellationToken cancellationToken)
    {
        var claim = await dbContext.Claims
            .AsNoTracking()
            .Include(candidate => candidate.TimelineEntries)
            .Include(candidate => candidate.WorkNotes)
            .Include(candidate => candidate.InformationRequests)
            .Include(candidate => candidate.Documents)
            .SingleOrDefaultAsync(candidate => candidate.Id == claimId, cancellationToken);

        if (claim is null)
            return null;

        return new ClaimAdjudicationDetailResult(
            claim.Id,
            claim.ClaimNumber,
            claim.PolicyId,
            claim.PolicyNumberAtFiling,
            claim.OwnerUserId,
            claim.IncidentType.ToString(),
            claim.IncidentAtUtc,
            claim.DiscoveredAtUtc,
            claim.Description,
            claim.Status.ToString(),
            claim.AssignedAdjusterUserId,
            claim.PolicyLimitAtFiling,
            claim.PolicyRetentionAtFiling,
            claim.PolicyEffectiveAtFiling,
            claim.PolicyExpirationAtFiling,
            claim.FiledAtUtc,
            claim.UpdatedAtUtc,
            claim.WorkNotes
                .OrderBy(note => note.CreatedAtUtc)
                .Select(ClaimAdjudicationResultFactory.FromWorkNote)
                .ToArray(),
            claim.InformationRequests
                .OrderBy(request => request.RequestedAtUtc)
                .Select(ClaimAdjudicationResultFactory.FromInformationRequest)
                .ToArray(),
            claim.Documents
                .OrderBy(document => document.UploadedAtUtc)
                .Select(ClaimDocumentResultFactory.FromDocument)
                .ToArray(),
            claim.TimelineEntries
                .OrderBy(entry => entry.CreatedAtUtc)
                .Select(entry => new ClaimTimelineEntryResult(
                    entry.Id,
                    entry.EntryType.ToString(),
                    entry.Summary,
                    entry.CreatedByUserId,
                    entry.CreatedAtUtc))
                .ToArray());
    }
}
