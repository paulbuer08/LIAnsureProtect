using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Application.Commands.DecideClaim;
using LIAnsureProtect.Modules.Claims.Application.Commands.ManageClaimFinancials;
using LIAnsureProtect.Modules.Claims.Application.Documents;
using LIAnsureProtect.Modules.Claims.Domain;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;

/// <summary>Role-scoped no-tracking reads for the adjuster's queue and file view.</summary>
public sealed class ClaimsAdjudicationReader(ClaimsDbContext dbContext) : IClaimsAdjudicationReader
{
    public async Task<IReadOnlyList<ClaimAdjudicationResult>> ListQueueAsync(CancellationToken cancellationToken)
    {
        // Pure SQL projection: the queue needs a count of open questions, not every
        // information-request row materialized per claim.
        return await dbContext.Claims
            .AsNoTracking()
            .Where(claim => claim.Status != ClaimStatus.Closed)
            .OrderByDescending(claim => claim.FiledAtUtc)
            .Select(claim => new ClaimAdjudicationResult(
                claim.Id,
                claim.ClaimNumber,
                claim.PolicyId,
                claim.PolicyNumberAtFiling,
                claim.IncidentType.ToString(),
                claim.IncidentAtUtc,
                claim.Status.ToString(),
                claim.AssignedAdjusterUserId,
                claim.InformationRequests.Count(request => !request.IsAnswered),
                claim.FiledAtUtc,
                claim.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
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
            .Include(candidate => candidate.ReserveChanges)
            .Include(candidate => candidate.Decisions)
            .AsSplitQuery()
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
            claim.ClaimedAmount,
            claim.ReserveAmount,
            claim.PaidAmount,
            claim.SettlementAmount,
            claim.DenialReason?.ToString(),
            claim.DenialNarrative,
            claim.DecidedAtUtc,
            claim.ClosedAtUtc,
            claim.PolicyLimitAtFiling,
            claim.PolicyRetentionAtFiling,
            claim.PolicyEffectiveAtFiling,
            claim.PolicyExpirationAtFiling,
            claim.FiledAtUtc,
            claim.UpdatedAtUtc,
            claim.ReserveChanges
                .OrderBy(change => change.ChangedAtUtc)
                .Select(ClaimFinancialsResultFactory.FromReserveChange)
                .ToArray(),
            claim.Decisions
                .OrderBy(decision => decision.DecidedAtUtc)
                .Select(decision => ClaimDecisionResultFactory.FromDecision(claim, decision))
                .ToArray(),
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
