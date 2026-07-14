namespace LIAnsureProtect.Modules.Underwriting.Application;

/// <summary>
/// Cross-context read port: the Underwriting module reads a read-only snapshot of a quote (owned by the
/// Quoting context) to build AI review context. Implemented on the legacy/Quoting side. The module never
/// mutates the quote — the underwriting decision stays with the Quote aggregate.
/// </summary>
public interface IUnderwritingQuoteContextReader
{
    Task<UnderwritingQuoteContext?> GetForAiReviewAsync(Guid quoteId, CancellationToken cancellationToken);

    /// <summary>Minimal quote facts needed to create a referral operation (risk tier, referred-at, expiry).</summary>
    Task<ReferralQuoteContext?> GetForReferralOperationAsync(Guid quoteId, CancellationToken cancellationToken);

    Task<QuoteAssuranceRequirementContext?> GetForAssuranceAsync(
        Guid quoteId,
        CancellationToken cancellationToken);
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
    int Version,
    string StrategyName,
    IReadOnlyCollection<string> Subjectivities,
    IReadOnlyCollection<string> ReferralReasons,
    IReadOnlyCollection<string> PriorUnderwritingDecisions,
    string SubmissionReference = "",
    string CompanyName = "");

/// <summary>Read-only quote facts for creating a referral operation. RiskTier is a string (cross-context).</summary>
public sealed record ReferralQuoteContext(
    Guid QuoteId,
    string RiskTier,
    DateTime ReferredAtUtc,
    DateTime ExpiresAtUtc);

public sealed record QuoteAssuranceRequirementContext(
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    IReadOnlyCollection<QuoteAssuranceRequirement> Requirements,
    string SubmissionReference = "",
    string CompanyName = "");

public sealed record QuoteAssuranceRequirement(
    string Category,
    bool EvidenceRequired,
    string Reason);
