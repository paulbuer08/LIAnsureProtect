using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.ListOwnerEvidenceRequests;

public sealed record ListOwnerEvidenceRequestsQuery : IRequest<ListOwnerEvidenceRequestsResult>;

public sealed class ListOwnerEvidenceRequestsQueryHandler(
    IEvidenceRequestsReader reader,
    IEvidenceDocumentRepository evidenceDocumentRepository,
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
        var evidenceRequestIds = evidenceRequests
            .Select(evidenceRequest => evidenceRequest.EvidenceRequestId)
            .ToList();
        var documents = await evidenceDocumentRepository.ListForRequestsAsync(evidenceRequestIds, cancellationToken);
        var documentsByRequestId = documents.ToLookup(document => document.EvidenceRequestId);

        return new ListOwnerEvidenceRequestsResult(
            evidenceRequests
                .Select(evidenceRequest => QuoteEvidenceRequestResultFactory.FromOwnerItem(
                    evidenceRequest,
                    documentsByRequestId[evidenceRequest.EvidenceRequestId].ToList()))
                .ToList());
    }
}
