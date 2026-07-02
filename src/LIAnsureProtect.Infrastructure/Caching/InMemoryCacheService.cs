using LIAnsureProtect.Platform.Abstractions.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace LIAnsureProtect.Infrastructure.Caching;

/// <summary>
/// In-process cache adapter backed by <see cref="IMemoryCache"/>. Used under
/// <c>Platform:Profile=Local</c> (and as the natural single-instance fallback).
/// </summary>
public sealed class InMemoryCacheService(IMemoryCache memoryCache) : ICacheService
{
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        var value = await factory(cancellationToken);
        memoryCache.Set(key, value, ttl);
        return value;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        memoryCache.Remove(key);
        return Task.CompletedTask;
    }
}
