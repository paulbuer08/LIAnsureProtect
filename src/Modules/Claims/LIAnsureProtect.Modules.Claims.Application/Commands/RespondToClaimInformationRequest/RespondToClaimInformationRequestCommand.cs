using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Claims.Application.Commands.RespondToClaimInformationRequest;

/// <summary>
/// The claimant answers an adjuster's information request. Owner-scoped: a claim that is missing
/// or owned by someone else returns null (→ 404, no existence leak).
/// </summary>
public sealed record RespondToClaimInformationRequestCommand(
    Guid ClaimId,
    Guid InformationRequestId,
    string ResponseText) : IRequest<ClaimInformationRequestResult?>;

public sealed class RespondToClaimInformationRequestCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser)
    : IRequestHandler<RespondToClaimInformationRequestCommand, ClaimInformationRequestResult?>
{
    public async Task<ClaimInformationRequestResult?> Handle(
        RespondToClaimInformationRequestCommand request,
        CancellationToken cancellationToken)
    {
        var claimantUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to respond to an information request.")
            : currentUser.UserId;

        var claim = await claims.GetByIdForUpdateAsync(request.ClaimId, cancellationToken);
        if (claim is null || !string.Equals(claim.OwnerUserId, claimantUserId, StringComparison.Ordinal))
            return null;

        claim.RespondToInformationRequest(
            request.InformationRequestId,
            claimantUserId,
            request.ResponseText,
            DateTime.UtcNow);
        await claims.SaveChangesAsync(cancellationToken);

        var informationRequest = claim.InformationRequests
            .Single(candidate => candidate.Id == request.InformationRequestId);

        return ClaimAdjudicationResultFactory.FromInformationRequest(informationRequest);
    }
}
