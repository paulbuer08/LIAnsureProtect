using MediatR;

namespace LIAnsureProtect.Modules.Claims.Application.Queries.ListClaimsForAdjudication;

/// <summary>The adjuster's queue: every open claim, newest filed first.</summary>
public sealed record ListClaimsForAdjudicationQuery : IRequest<ListClaimsForAdjudicationResult>;

public sealed record ListClaimsForAdjudicationResult(IReadOnlyList<ClaimAdjudicationResult> Claims);

public sealed class ListClaimsForAdjudicationQueryHandler(IClaimsAdjudicationReader reader)
    : IRequestHandler<ListClaimsForAdjudicationQuery, ListClaimsForAdjudicationResult>
{
    public async Task<ListClaimsForAdjudicationResult> Handle(
        ListClaimsForAdjudicationQuery request,
        CancellationToken cancellationToken)
    {
        var claims = await reader.ListQueueAsync(cancellationToken);

        return new ListClaimsForAdjudicationResult(claims);
    }
}
