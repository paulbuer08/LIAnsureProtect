using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Modules.Quoting.Application.ReferralDecisions;

namespace LIAnsureProtect.Infrastructure.Quotes;

public sealed class QuoteReferralDecisionService(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork) : IQuoteReferralDecisionService
{
    public async Task<UnderwriteQuoteReferralResult?> ApproveAsync(
        Guid quoteId,
        string reviewedByUserId,
        string reason,
        string? notes,
        DateTime reviewedAtUtc,
        CancellationToken cancellationToken)
    {
        var quote = await quoteRepository.GetForUnderwritingReviewAsync(quoteId, cancellationToken);
        if (quote is null)
            return null;

        var review = quote.ApproveReferral(reviewedByUserId, reason, notes, reviewedAtUtc);
        await quoteRepository.AddUnderwritingReviewAsync(review, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return FromQuote(quote);
    }

    public async Task<UnderwriteQuoteReferralResult?> DeclineAsync(
        Guid quoteId,
        string reviewedByUserId,
        string reason,
        string? notes,
        DateTime reviewedAtUtc,
        CancellationToken cancellationToken)
    {
        var quote = await quoteRepository.GetForUnderwritingReviewAsync(quoteId, cancellationToken);
        if (quote is null)
            return null;

        var review = quote.DeclineReferral(reviewedByUserId, reason, notes, reviewedAtUtc);
        await quoteRepository.AddUnderwritingReviewAsync(review, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return FromQuote(quote);
    }

    public async Task<UnderwriteQuoteReferralResult?> AdjustAsync(
        Guid quoteId,
        string reviewedByUserId,
        decimal adjustedPremium,
        decimal adjustedRetention,
        string? updatedSubjectivities,
        string reason,
        string? notes,
        DateTime reviewedAtUtc,
        CancellationToken cancellationToken)
    {
        var quote = await quoteRepository.GetForUnderwritingReviewAsync(quoteId, cancellationToken);
        if (quote is null)
            return null;

        var review = quote.AdjustReferral(
            reviewedByUserId,
            adjustedPremium,
            adjustedRetention,
            updatedSubjectivities,
            reason,
            notes,
            reviewedAtUtc);
        await quoteRepository.AddUnderwritingReviewAsync(review, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return FromQuote(quote);
    }

    private static UnderwriteQuoteReferralResult FromQuote(Quote quote)
    {
        return new UnderwriteQuoteReferralResult(
            quote.Id,
            quote.SubmissionId,
            quote.Status.ToString(),
            quote.Premium,
            quote.RequestedLimit,
            quote.Retention,
            quote.ReviewedByUserId ?? string.Empty,
            quote.ReviewedAtUtc ?? throw new InvalidOperationException("Reviewed quote must have a review timestamp."),
            quote.UnderwritingDecisionReason ?? string.Empty,
            quote.UnderwritingDecisionNotes);
    }
}
