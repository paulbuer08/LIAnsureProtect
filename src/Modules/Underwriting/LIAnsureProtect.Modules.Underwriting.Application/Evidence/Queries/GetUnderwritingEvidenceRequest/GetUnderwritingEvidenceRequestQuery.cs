using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;
using MediatR;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.GetUnderwritingEvidenceRequest;

public sealed record GetUnderwritingEvidenceRequestQuery(Guid QuoteId, Guid EvidenceRequestId)
    : IRequest<QuoteEvidenceRequestResult?>;

public sealed class GetUnderwritingEvidenceRequestQueryHandler(
    IEvidenceRequestsReader reader,
    IEvidenceRequestRepository evidenceRequestRepository,
    IEvidenceDocumentRepository evidenceDocumentRepository)
    : IRequestHandler<GetUnderwritingEvidenceRequestQuery, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        GetUnderwritingEvidenceRequestQuery request,
        CancellationToken cancellationToken)
    {
        var item = await reader.GetUnderwritingRequestAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (item is null)
            return null;

        var documents = await evidenceDocumentRepository.ListForRequestsAsync(
            [item.EvidenceRequestId],
            cancellationToken);
        var responses = await evidenceRequestRepository.ListResponsesAsync(
            item.EvidenceRequestId,
            cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromSnapshot(item, documents, responses);
    }
}
