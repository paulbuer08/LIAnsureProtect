using LIAnsureProtect.Modules.Underwriting.Domain.Evidence.Documents;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;

public interface IEvidenceDocumentRepository
{
    Task AddDocumentsAsync(
        IReadOnlyCollection<QuoteEvidenceDocument> evidenceDocuments,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<QuoteEvidenceDocument>> ListForRequestsAsync(
        IReadOnlyCollection<Guid> evidenceRequestIds,
        CancellationToken cancellationToken);

    Task<QuoteEvidenceDocument?> GetForOwnerAsync(
        Guid evidenceRequestId,
        Guid documentId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<QuoteEvidenceDocument?> GetForUnderwritingAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        Guid documentId,
        CancellationToken cancellationToken);
}
