using LIAnsureProtect.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace LIAnsureProtect.IntegrationTests.Caching;

/// <summary>
/// Unit tests for the in-memory cache adapter: hit/miss (the factory runs once), invalidation via
/// <see cref="Platform.Abstractions.Caching.ICacheService.RemoveAsync"/> (the factory re-runs after
/// eviction), and TTL expiry. No network — runs in the normal test/CI path.
/// </summary>
public sealed class InMemoryCacheServiceTests
{
    private static InMemoryCacheService CreateService() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task GetOrCreateAsync_Builds_On_Miss_And_Returns_Cached_On_Hit()
    {
        var service = CreateService();
        var factoryCalls = 0;

        var first = await service.GetOrCreateAsync(
            "queue:summary", _ => { factoryCalls++; return Task.FromResult(41); },
            TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        var second = await service.GetOrCreateAsync(
            "queue:summary", _ => { factoryCalls++; return Task.FromResult(99); },
            TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        Assert.Equal(41, first);
        Assert.Equal(41, second); // served from cache, not rebuilt
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task RemoveAsync_Evicts_So_The_Next_Read_Rebuilds()
    {
        var service = CreateService();
        var factoryCalls = 0;

        await service.GetOrCreateAsync(
            "queue:summary", _ => { factoryCalls++; return Task.FromResult(1); },
            TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        await service.RemoveAsync("queue:summary", TestContext.Current.CancellationToken);

        var afterInvalidation = await service.GetOrCreateAsync(
            "queue:summary", _ => { factoryCalls++; return Task.FromResult(2); },
            TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        Assert.Equal(2, afterInvalidation);
        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public async Task GetOrCreateAsync_Rebuilds_After_Ttl_Expires()
    {
        var service = CreateService();
        var factoryCalls = 0;

        await service.GetOrCreateAsync(
            "queue:summary", _ => { factoryCalls++; return Task.FromResult(1); },
            TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);

        await Task.Delay(60, TestContext.Current.CancellationToken);

        var rebuilt = await service.GetOrCreateAsync(
            "queue:summary", _ => { factoryCalls++; return Task.FromResult(2); },
            TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);

        Assert.Equal(2, rebuilt);
        Assert.Equal(2, factoryCalls);
    }
}
