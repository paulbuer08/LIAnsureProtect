namespace LIAnsureProtect.Application.Common.Caching;

/// <summary>
/// A request opts into cache-aside by implementing this. Only mark <b>rebuildable, non-PII</b>
/// reads, and pair adoption with invalidation on the write paths that change the data.
/// </summary>
public interface ICacheableRequest
{
    /// <summary>Stable cache key for this request (include a version segment, e.g. <c>referrals:v1</c>).</summary>
    string CacheKey { get; }

    /// <summary>How long the cached response stays fresh.</summary>
    TimeSpan CacheTtl { get; }
}
