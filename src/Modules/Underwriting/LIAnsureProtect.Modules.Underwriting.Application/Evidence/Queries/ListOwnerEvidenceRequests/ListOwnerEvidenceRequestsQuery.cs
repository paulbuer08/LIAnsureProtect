using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.ListOwnerEvidenceRequests;

public sealed record ListOwnerEvidenceRequestsQuery : IRequest<ListOwnerEvidenceRequestsResult>;

public sealed class ListOwnerEvidenceRequestsQueryHandler(
    IEvidenceRequestsReader reader,
    ICurrentUser currentUser)
    : IRequestHandler<ListOwnerEvidenceRequestsQuery, ListOwnerEvidenceRequestsResult>
{
    public async Task<ListOwnerEvidenceRequestsResult> Handle(
        ListOwnerEvidenceRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = CurrentEvidenceUser.GetRequiredUserId(
            currentUser,
            "An authenticated owner user id is required to list evidence requests.");
        var evidenceRequests = await reader.GetOwnerRequestsAsync(ownerUserId, cancellationToken);

        return new ListOwnerEvidenceRequestsResult(
            evidenceRequests
                .Select(QuoteEvidenceRequestResultFactory.FromOwnerItem)
                .ToList());
    }
}
