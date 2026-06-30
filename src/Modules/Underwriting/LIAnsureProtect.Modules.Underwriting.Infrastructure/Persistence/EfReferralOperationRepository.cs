using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals;
using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class EfReferralOperationRepository(
    UnderwritingDbContext dbContext,
    IUnderwritingQuoteContextReader quoteContextReader)
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

    public async Task<QuoteReferralOperation?> EnsureExistsForQuoteAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        var operation = await GetByQuoteIdForUpdateAsync(quoteId, cancellationToken);
        if (operation is not null)
            return operation;

        var quote = await quoteContextReader.GetForReferralOperationAsync(quoteId, cancellationToken);
        if (quote is null)
            return null;

        operation = QuoteReferralOperation.CreateDefault(
            quote.QuoteId, quote.RiskTier, quote.ReferredAtUtc, quote.ExpiresAtUtc);
        await dbContext.QuoteReferralOperations.AddAsync(operation, cancellationToken);
        return operation;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
