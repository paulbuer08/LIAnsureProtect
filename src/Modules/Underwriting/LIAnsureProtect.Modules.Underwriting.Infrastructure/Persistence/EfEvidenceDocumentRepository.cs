using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence.Documents;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

public sealed class EfEvidenceDocumentRepository(UnderwritingDbContext dbContext) : IEvidenceDocumentRepository
{
    public async Task AddDocumentsAsync(
        IReadOnlyCollection<QuoteEvidenceDocument> evidenceDocuments,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<QuoteEvidenceDocument>().AddRangeAsync(evidenceDocuments, cancellationToken);
    }

    public async Task<IReadOnlyCollection<QuoteEvidenceDocument>> ListForRequestsAsync(
        IReadOnlyCollection<Guid> evidenceRequestIds,
        CancellationToken cancellationToken)
    {
        if (evidenceRequestIds.Count == 0)
            return [];

        return await dbContext.Set<QuoteEvidenceDocument>()
            .AsNoTracking()
            .Where(document => evidenceRequestIds.Contains(document.EvidenceRequestId))
            .OrderBy(document => document.UploadedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<QuoteEvidenceDocument?> GetForOwnerAsync(
        Guid evidenceRequestId,
        Guid documentId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<QuoteEvidenceDocument>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                document => document.Id == documentId
                    && document.EvidenceRequestId == evidenceRequestId
                    && document.OwnerUserId == ownerUserId,
                cancellationToken);
    }

    public async Task<QuoteEvidenceDocument?> GetForUnderwritingAsync(
        Guid quoteId,
        Guid evidenceRequestId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<QuoteEvidenceDocument>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                document => document.Id == documentId
                    && document.EvidenceRequestId == evidenceRequestId
                    && document.QuoteId == quoteId,
                cancellationToken);
    }
}
