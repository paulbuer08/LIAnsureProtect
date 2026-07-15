using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Modules.Underwriting.Application;
using LIAnsureProtect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Quotes;

/// <summary>
/// Legacy/Quoting-side adapter for the Underwriting module's <see cref="IUnderwritingQuoteContextReader"/>
/// port. It reads a read-only snapshot of the quote and its prior underwriting decisions (both owned by
/// the Quoting context) so the Underwriting module can build AI review context without referencing the
/// Quote aggregate or its tables.
/// </summary>
public sealed class QuoteUnderwritingContextReader(
    IQuoteRepository quoteRepository,
    SubmissionDbContext dbContext)
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
        var identity = await GetSubmissionIdentityAsync(quote.SubmissionId, quote.OwnerUserId, cancellationToken);

        return new UnderwritingQuoteContext(
            quote.Id,
            quote.SubmissionId,
            quote.OwnerUserId,
            quote.Premium,
            quote.RequestedLimit,
            quote.Retention,
            quote.RiskTier.ToString(),
            quote.Status.ToString(),
            quote.Version,
            quote.StrategyName,
            SplitLines(quote.Subjectivities),
            SplitLines(quote.ReferralReasons),
            (priorReviews ?? [])
                .OrderBy(review => review.CreatedAtUtc)
                .Select(review => $"{review.Decision}: {review.Reason}")
                .ToArray(),
            identity.Reference,
            identity.CompanyName);
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

        var identity = await GetSubmissionIdentityAsync(quote.SubmissionId, quote.OwnerUserId, cancellationToken);

        return new QuoteAssuranceRequirementContext(
            quote.Id,
            quote.SubmissionId,
            quote.OwnerUserId,
            quote.Version,
            quote.SupersedesQuoteId,
            quote.ControlAssertions
                .Select(assertion => new QuoteAssuranceRequirement(
                    ToEvidenceCategory(assertion.ControlType),
                    assertion.EvidenceRequired,
                    assertion.EvidenceReason))
                .ToArray(),
            identity.Reference,
            identity.CompanyName);
    }

    private async Task<(string Reference, string CompanyName)> GetSubmissionIdentityAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var identity = await dbContext.Submissions
            .AsNoTracking()
            .Where(submission => submission.Id == submissionId && submission.OwnerUserId == ownerUserId)
            .Select(submission => new { submission.Reference, submission.CompanyName })
            .SingleOrDefaultAsync(cancellationToken);

        return identity is null
            ? ($"SUB-LEGACY-{submissionId:N}"[..30], "Company not provided")
            : (identity.Reference, identity.CompanyName);
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
