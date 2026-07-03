using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.GetEvidenceReferenceData;
using LIAnsureProtect.Platform.Abstractions.Caching;

namespace LIAnsureProtect.UnitTests.Underwriting;

/// <summary>
/// The evidence reference-data query is the first production adoption of cache-aside: it must opt
/// in via <see cref="ICacheableRequest"/> with a stable versioned key, and its handler must expose
/// the same categories and upload rules the workflows actually enforce (single source of truth).
/// </summary>
public sealed class GetEvidenceReferenceDataQueryTests
{
    [Fact]
    public void Query_Opts_Into_Caching_With_A_Stable_Versioned_Key()
    {
        var query = new GetEvidenceReferenceDataQuery();

        var cacheable = Assert.IsAssignableFrom<ICacheableRequest>(query);
        Assert.Equal("underwriting:evidence-reference:v1", cacheable.CacheKey);
        Assert.Equal(TimeSpan.FromHours(1), cacheable.CacheTtl);
    }

    [Fact]
    public async Task Handler_Returns_Categories_And_The_Enforced_Upload_Rules()
    {
        var handler = new GetEvidenceReferenceDataQueryHandler();

        var result = await handler.Handle(new GetEvidenceReferenceDataQuery(), CancellationToken.None);

        Assert.Contains("MultiFactorAuthentication", result.Categories);
        Assert.Contains(result.AllowedContentTypes, allowed =>
            allowed.ContentType == "application/pdf" && allowed.Extension == ".pdf");
        Assert.Equal(5, result.MaximumDocumentCount);
        Assert.Equal(10 * 1024 * 1024, result.MaximumDocumentSizeBytes);
        Assert.Equal(50 * 1024 * 1024, result.MaximumTotalDocumentSizeBytes);
    }
}
