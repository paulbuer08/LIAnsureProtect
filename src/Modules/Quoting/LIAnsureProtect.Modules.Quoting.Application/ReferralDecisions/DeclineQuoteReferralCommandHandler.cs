using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Quoting.Application.ReferralDecisions;

public sealed class DeclineQuoteReferralCommandHandler(
    IQuoteReferralDecisionService decisionService,
    ICurrentUser currentUser)
    : IRequestHandler<DeclineQuoteReferralCommand, UnderwriteQuoteReferralResult?>
{
    public async Task<UnderwriteQuoteReferralResult?> Handle(
        DeclineQuoteReferralCommand request,
        CancellationToken cancellationToken)
    {
        return await decisionService.DeclineAsync(
            request.QuoteId,
            GetRequiredCurrentUserId(),
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
