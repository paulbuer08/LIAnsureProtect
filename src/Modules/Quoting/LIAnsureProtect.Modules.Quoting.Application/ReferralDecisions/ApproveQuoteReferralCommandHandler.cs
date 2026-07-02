using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Quoting.Application.ReferralDecisions;

public sealed class ApproveQuoteReferralCommandHandler(
    IQuoteReferralDecisionService decisionService,
    ICurrentUser currentUser)
    : IRequestHandler<ApproveQuoteReferralCommand, UnderwriteQuoteReferralResult?>
{
    public async Task<UnderwriteQuoteReferralResult?> Handle(
        ApproveQuoteReferralCommand request,
        CancellationToken cancellationToken)
    {
        return await decisionService.ApproveAsync(
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
