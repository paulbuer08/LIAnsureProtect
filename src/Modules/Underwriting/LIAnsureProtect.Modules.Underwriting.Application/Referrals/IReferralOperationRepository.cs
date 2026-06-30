using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;

namespace LIAnsureProtect.Modules.Underwriting.Application.Referrals;

/// <summary>
/// Module-owned persistence for the referral operation aggregate. Writes commit through the module's
/// own DbContext (no shared unit of work), mirroring the M35 AI-review repository.
/// </summary>
public interface IReferralOperationRepository
{
    Task AddAsync(QuoteReferralOperation operation, CancellationToken cancellationToken);

    /// <summary>Loads the tracked aggregate (with notes/tasks/timeline) for mutation, or null.</summary>
    Task<QuoteReferralOperation?> GetByQuoteIdForUpdateAsync(Guid quoteId, CancellationToken cancellationToken);

    /// <summary>True if an operation already exists for the quote (create-if-missing idempotency).</summary>
    Task<bool> ExistsForQuoteAsync(Guid quoteId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the operation for the quote, or creates it (create-if-missing) via the quote read-back when
    /// absent — so an underwriter action arriving before the QuoteGenerated projector runs self-heals
    /// instead of 404ing. Does NOT save (the caller's SaveChangesAsync commits). Returns null only when
    /// the quote itself does not exist.
    /// </summary>
    Task<QuoteReferralOperation?> EnsureExistsForQuoteAsync(Guid quoteId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
