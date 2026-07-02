using System.Text.Json;
using LIAnsureProtect.Platform.Abstractions.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace LIAnsureProtect.Infrastructure.Caching;

/// <summary>
/// Distributed cache adapter backed by Redis via <see cref="IDistributedCache"/>. Values are
/// JSON-serialized and keys are prefixed so multiple apps can share one Redis safely. Used under
/// <c>Platform:Profile=Aws</c> (ElastiCache in real deployments, local Docker Redis in dev).
/// </summary>
public sealed class RedisCacheService(
    IDistributedCache distributedCache,
    IOptions<CacheOptions> options) : ICacheService
{
    private readonly string keyPrefix = string.IsNullOrWhiteSpace(options.Value.KeyPrefix)
        ? "liap:"
        : options.Value.KeyPrefix;

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        var fullKey = keyPrefix + key;

        var cached = await distributedCache.GetStringAsync(fullKey, cancellationToken);
        if (cached is not null)
        {
            var deserialized = JsonSerializer.Deserialize<T>(cached, JsonSerializerOptions.Web);
            if (deserialized is not null)
                return deserialized;
        }

        var value = await factory(cancellationToken);
        var serialized = JsonSerializer.Serialize(value, JsonSerializerOptions.Web);
        await distributedCache.SetStringAsync(
            fullKey,
            serialized,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            cancellationToken);

        return value;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        return distributedCache.RemoveAsync(keyPrefix + key, cancellationToken);
    }
}
