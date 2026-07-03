using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Claims.Application.Queries.GetMyClaimDetail;

/// <summary>Owner-scoped claim detail; null (→ 404) when missing or owned by someone else.</summary>
public sealed record GetMyClaimDetailQuery(Guid ClaimId) : IRequest<ClaimDetailResult?>;

public sealed class GetMyClaimDetailQueryHandler(
    IClaimsReader claimsReader,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyClaimDetailQuery, ClaimDetailResult?>
{
    public async Task<ClaimDetailResult?> Handle(GetMyClaimDetailQuery request, CancellationToken cancellationToken)
    {
        var ownerUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to read a claim.")
            : currentUser.UserId;

        return await claimsReader.GetOwnerClaimDetailAsync(ownerUserId, request.ClaimId, cancellationToken);
    }
}
