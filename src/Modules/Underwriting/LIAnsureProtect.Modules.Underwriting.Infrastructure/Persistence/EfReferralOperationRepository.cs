using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class EfReferralOperationRepository(UnderwritingDbContext dbContext)
    : IReferralOperationRepository
{
    public async Task AddAsync(QuoteReferralOperation operation, CancellationToken cancellationToken)
        => await dbContext.QuoteReferralOperations.AddAsync(operation, cancellationToken);

    public Task<QuoteReferralOperation?> GetByQuoteIdForUpdateAsync(Guid quoteId, CancellationToken cancellationToken)
        => dbContext.QuoteReferralOperations
            .Include(operation => operation.Notes)
            .Include(operation => operation.Tasks)
            .Include(operation => operation.TimelineEntries)
            .SingleOrDefaultAsync(operation => operation.QuoteId == quoteId, cancellationToken);

    public Task<bool> ExistsForQuoteAsync(Guid quoteId, CancellationToken cancellationToken)
        => dbContext.QuoteReferralOperations.AnyAsync(operation => operation.QuoteId == quoteId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
