namespace LIAnsureProtect.Modules.Quoting.Application.ReferralDecisions;

public interface IQuoteReferralDecisionService
{
    Task<UnderwriteQuoteReferralResult?> ApproveAsync(
        Guid quoteId,
        string reviewedByUserId,
        string reason,
        string? notes,
        DateTime reviewedAtUtc,
        CancellationToken cancellationToken);

    Task<UnderwriteQuoteReferralResult?> DeclineAsync(
        Guid quoteId,
        string reviewedByUserId,
        string reason,
        string? notes,
        DateTime reviewedAtUtc,
        CancellationToken cancellationToken);

    Task<UnderwriteQuoteReferralResult?> AdjustAsync(
        Guid quoteId,
        string reviewedByUserId,
        decimal adjustedPremium,
        decimal adjustedRetention,
        string? updatedSubjectivities,
        string reason,
        string? notes,
        DateTime reviewedAtUtc,
        CancellationToken cancellationToken);
}
