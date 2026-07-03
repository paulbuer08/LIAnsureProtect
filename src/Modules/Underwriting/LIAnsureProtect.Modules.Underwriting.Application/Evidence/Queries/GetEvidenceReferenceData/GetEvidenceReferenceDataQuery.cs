using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using LIAnsureProtect.Platform.Abstractions.Caching;
using MediatR;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.GetEvidenceReferenceData;

/// <summary>
/// Reference data for the evidence UI: request categories and the document upload rules.
/// This is the first production adoption of cache-aside (M44 mechanism): the data is rebuildable,
/// non-PII, and changes only when code changes — the textbook cache candidate. The versioned key
/// means a future rules change ships with a key bump (v2) instead of an invalidation dance.
/// </summary>
public sealed record GetEvidenceReferenceDataQuery
    : IRequest<EvidenceReferenceDataResult>, ICacheableRequest
{
    public string CacheKey => "underwriting:evidence-reference:v1";

    public TimeSpan CacheTtl => TimeSpan.FromHours(1);
}

public sealed record EvidenceReferenceDataResult(
    IReadOnlyCollection<string> Categories,
    IReadOnlyCollection<EvidenceAllowedContentTypeResult> AllowedContentTypes,
    int MaximumDocumentCount,
    long MaximumDocumentSizeBytes,
    long MaximumTotalDocumentSizeBytes);

public sealed record EvidenceAllowedContentTypeResult(string ContentType, string Extension);

public sealed class GetEvidenceReferenceDataQueryHandler
    : IRequestHandler<GetEvidenceReferenceDataQuery, EvidenceReferenceDataResult>
{
    public Task<EvidenceReferenceDataResult> Handle(
        GetEvidenceReferenceDataQuery request,
        CancellationToken cancellationToken)
    {
        var result = new EvidenceReferenceDataResult(
            Enum.GetNames<EvidenceRequestCategory>(),
            EvidenceDocumentUploadRules.AllowedExtensionsByContentType
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new EvidenceAllowedContentTypeResult(pair.Key, pair.Value))
                .ToList(),
            EvidenceDocumentUploadRules.MaximumDocumentCount,
            EvidenceDocumentUploadRules.MaximumDocumentSizeBytes,
            EvidenceDocumentUploadRules.MaximumTotalDocumentSizeBytes);

        return Task.FromResult(result);
    }
}
