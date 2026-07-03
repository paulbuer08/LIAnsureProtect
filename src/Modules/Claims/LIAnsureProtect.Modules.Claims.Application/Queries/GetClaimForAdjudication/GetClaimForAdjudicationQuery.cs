using MediatR;

namespace LIAnsureProtect.Modules.Claims.Application.Queries.GetClaimForAdjudication;

/// <summary>The adjuster's full working view of one claim (notes, information requests, timeline).</summary>
public sealed record GetClaimForAdjudicationQuery(Guid ClaimId) : IRequest<ClaimAdjudicationDetailResult?>;

public sealed class GetClaimForAdjudicationQueryHandler(IClaimsAdjudicationReader reader)
    : IRequestHandler<GetClaimForAdjudicationQuery, ClaimAdjudicationDetailResult?>
{
    public Task<ClaimAdjudicationDetailResult?> Handle(
        GetClaimForAdjudicationQuery request,
        CancellationToken cancellationToken)
        => reader.GetDetailAsync(request.ClaimId, cancellationToken);
}
