using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Modules.Underwriting.Application;

namespace LIAnsureProtect.Infrastructure.Quotes;

/// <summary>
/// Legacy/Quoting-side adapter for the Underwriting module's <see cref="IUnderwritingQuoteContextReader"/>
/// port. It reads a read-only snapshot of the quote and its prior underwriting decisions (both owned by
/// the Quoting context) so the Underwriting module can build AI review context without referencing the
/// Quote aggregate or its tables.
/// </summary>
public sealed class QuoteUnderwritingContextReader(IQuoteRepository quoteRepository)
    : IUnderwritingQuoteContextReader
{
    public async Task<UnderwritingQuoteContext?> GetForAiReviewAsync(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        var quote = await quoteRepository.GetForUnderwritingReviewAsync(quoteId, cancellationToken);
        if (quote is null)
            return null;

        var priorReviews = await quoteRepository.ListUnderwritingReviewsAsync(quoteId, cancellationToken);

        return new UnderwritingQuoteContext(
            quote.Id,
            quote.SubmissionId,
            quote.OwnerUserId,
            quote.Premium,
            quote.RequestedLimit,
            quote.Retention,
            quote.RiskTier.ToString(),
            quote.Status.ToString(),
            quote.StrategyName,
            SplitLines(quote.Subjectivities),
            SplitLines(quote.ReferralReasons),
            (priorReviews ?? [])
                .OrderBy(review => review.CreatedAtUtc)
                .Select(review => $"{review.Decision}: {review.Reason}")
                .ToArray());
    }

    public async Task<ReferralQuoteContext?> GetForReferralOperationAsync(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        var quote = await quoteRepository.GetForUnderwritingReviewAsync(quoteId, cancellationToken);
        if (quote is null)
            return null;

        return new ReferralQuoteContext(
            quote.Id,
            quote.RiskTier.ToString(),
            quote.CreatedAtUtc,
            quote.ExpiresAtUtc);
    }

    public async Task<QuoteAssuranceRequirementContext?> GetForAssuranceAsync(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        var quote = await quoteRepository.GetForUnderwritingReviewAsync(quoteId, cancellationToken);
        if (quote is null)
            return null;

        return new QuoteAssuranceRequirementContext(
            quote.Id,
            quote.SubmissionId,
            quote.OwnerUserId,
            quote.ControlAssertions
                .Select(assertion => new QuoteAssuranceRequirement(
                    ToEvidenceCategory(assertion.ControlType),
                    assertion.EvidenceRequired,
                    assertion.EvidenceReason))
                .ToArray());
    }

    private static string ToEvidenceCategory(ControlType controlType) => controlType switch
    {
        ControlType.MultiFactorAuthentication => "MultiFactorAuthentication",
        ControlType.EndpointDetectionAndResponse => "EndpointDetectionAndResponse",
        ControlType.BackupRecovery => "BackupRecovery",
        ControlType.IncidentResponsePlan => "IncidentResponsePlan",
        ControlType.SensitiveData => "SecurityQuestionnaireClarification",
        _ => "SecurityQuestionnaireClarification"
    };

    private static string[] SplitLines(string value)
    {
        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }
}
