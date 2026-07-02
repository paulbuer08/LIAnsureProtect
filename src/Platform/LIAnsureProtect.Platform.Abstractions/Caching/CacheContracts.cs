namespace LIAnsureProtect.Platform.Abstractions.Caching;

/// <summary>
/// Cache-aside abstraction. The adapter is chosen by the active deployment profile: in-memory
/// locally, Redis under the Aws profile. Only put <b>rebuildable, non-PII</b> data here.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or runs <paramref name="factory"/> to
    /// build it, stores it for <paramref name="ttl"/>, and returns it.
    /// </summary>
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken cancellationToken);

    /// <summary>Evicts <paramref name="key"/> so the next read rebuilds it (invalidation hook).</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}
