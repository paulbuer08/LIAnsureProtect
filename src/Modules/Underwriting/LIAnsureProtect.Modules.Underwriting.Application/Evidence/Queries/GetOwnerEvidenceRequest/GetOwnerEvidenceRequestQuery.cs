using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.GetOwnerEvidenceRequest;

public sealed record GetOwnerEvidenceRequestQuery(Guid EvidenceRequestId)
    : IRequest<QuoteEvidenceRequestResult?>;

public sealed class GetOwnerEvidenceRequestQueryHandler(
    IEvidenceRequestsReader reader,
    IEvidenceDocumentRepository evidenceDocumentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetOwnerEvidenceRequestQuery, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        GetOwnerEvidenceRequestQuery request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = CurrentEvidenceUser.GetRequiredUserId(
            currentUser,
            "An authenticated owner user id is required to view an evidence request.");
        var item = await reader.GetOwnerRequestAsync(
            request.EvidenceRequestId,
            ownerUserId,
            cancellationToken);
        if (item is null)
            return null;

        var documents = await evidenceDocumentRepository.ListForRequestsAsync(
            [item.EvidenceRequestId],
            cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromSnapshot(item, documents);
    }
}
