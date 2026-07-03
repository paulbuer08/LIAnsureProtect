using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Domain;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Persistence;

public sealed class EfClaimRepository(ClaimsDbContext dbContext) : IClaimRepository
{
    public async Task AddAsync(Claim claim, CancellationToken cancellationToken)
        => await dbContext.Claims.AddAsync(claim, cancellationToken);

    public Task<Claim?> GetByIdForUpdateAsync(Guid claimId, CancellationToken cancellationToken)
        => dbContext.Claims
            .Include(claim => claim.TimelineEntries)
            .Include(claim => claim.WorkNotes)
            .Include(claim => claim.InformationRequests)
            .Include(claim => claim.Documents)
            .SingleOrDefaultAsync(claim => claim.Id == claimId, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A racing writer updated the same claim row first (the Version concurrency token no
            // longer matched). Translate to the domain's conflict language so the API's existing
            // InvalidOperationException → 409 mapping applies.
            throw new InvalidOperationException(
                "This claim was just updated by someone else. Refresh and try again.");
        }
    }
}
