namespace LIAnsureProtect.Modules.Underwriting.Application;

/// <summary>
/// Cross-context read port: the Underwriting module reads a read-only snapshot of a quote (owned by the
/// Quoting context) to build AI review context. Implemented on the legacy/Quoting side. The module never
/// mutates the quote — the underwriting decision stays with the Quote aggregate.
/// </summary>
public interface IUnderwritingQuoteContextReader
{
    Task<UnderwritingQuoteContext?> GetForAiReviewAsync(Guid quoteId, CancellationToken cancellationToken);
}

/// <summary>Read-only snapshot of a quote plus its prior underwriting decisions, for AI review context.</summary>
public sealed record UnderwritingQuoteContext(
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    decimal Premium,
    decimal RequestedLimit,
    decimal Retention,
    string RiskTier,
    string Status,
    string StrategyName,
    IReadOnlyCollection<string> Subjectivities,
    IReadOnlyCollection<string> ReferralReasons,
    IReadOnlyCollection<string> PriorUnderwritingDecisions);
