using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.UnderwriteQuoteReferral;

public sealed class DeclineQuoteReferralCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<DeclineQuoteReferralCommand, UnderwriteQuoteReferralResult?>
{
    public async Task<UnderwriteQuoteReferralResult?> Handle(
        DeclineQuoteReferralCommand request,
        CancellationToken cancellationToken)
    {
        var quote = await quoteRepository.GetForUnderwritingReviewAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return null;

        var reviewedAtUtc = DateTime.UtcNow;
        var reviewedByUserId = GetRequiredCurrentUserId();
        var review = quote.DeclineReferral(
            reviewedByUserId,
            request.Reason,
            request.Notes,
            reviewedAtUtc);
        await quoteRepository.AddUnderwritingReviewAsync(review, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UnderwriteQuoteReferralResultFactory.FromQuote(quote);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated underwriter user id is required to review a quote.")
            : currentUser.UserId;
    }
}
