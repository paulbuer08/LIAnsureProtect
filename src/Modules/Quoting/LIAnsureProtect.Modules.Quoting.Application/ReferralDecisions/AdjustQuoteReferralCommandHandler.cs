using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Quoting.Application.ReferralDecisions;

public sealed class AdjustQuoteReferralCommandHandler(
    IQuoteReferralDecisionService decisionService,
    ICurrentUser currentUser)
    : IRequestHandler<AdjustQuoteReferralCommand, UnderwriteQuoteReferralResult?>
{
    public async Task<UnderwriteQuoteReferralResult?> Handle(
        AdjustQuoteReferralCommand request,
        CancellationToken cancellationToken)
    {
        return await decisionService.AdjustAsync(
            request.QuoteId,
            GetRequiredCurrentUserId(),
            request.AdjustedPremium,
            request.AdjustedRetention,
            request.UpdatedSubjectivities,
            request.Reason,
            request.Notes,
            DateTime.UtcNow,
            cancellationToken);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated underwriter user id is required to review a quote.")
            : currentUser.UserId;
    }
}
