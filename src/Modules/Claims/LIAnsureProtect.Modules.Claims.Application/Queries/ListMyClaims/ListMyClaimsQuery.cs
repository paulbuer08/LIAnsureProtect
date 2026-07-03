using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Claims.Application.Queries.ListMyClaims;

/// <summary>Owner-scoped claim list: callers only ever see their own claims.</summary>
public sealed record ListMyClaimsQuery : IRequest<ListMyClaimsResult>;

public sealed record ListMyClaimsResult(IReadOnlyList<ClaimResult> Claims);

public sealed class ListMyClaimsQueryHandler(
    IClaimsReader claimsReader,
    ICurrentUser currentUser)
    : IRequestHandler<ListMyClaimsQuery, ListMyClaimsResult>
{
    public async Task<ListMyClaimsResult> Handle(ListMyClaimsQuery request, CancellationToken cancellationToken)
    {
        var ownerUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to list claims.")
            : currentUser.UserId;

        var claims = await claimsReader.ListOwnerClaimsAsync(ownerUserId, cancellationToken);

        return new ListMyClaimsResult(claims);
    }
}
