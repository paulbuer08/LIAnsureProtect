using LIAnsureProtect.Application.Common.Behaviors;
using LIAnsureProtect.Application.Common.Caching;
using LIAnsureProtect.Platform.Abstractions.Caching;
using MediatR;

namespace LIAnsureProtect.UnitTests.Caching;

/// <summary>
/// The caching pipeline behavior caches only requests that opt in via
/// <see cref="ICacheableRequest"/>; everything else passes straight through to the handler.
/// Uses a dictionary-backed fake cache so the test exercises the behavior's routing, not a real
/// cache backend (that is covered by the adapter tests).
/// </summary>
public sealed class CachingBehaviorTests
{
    private sealed record CacheableQuery(string CacheKey, TimeSpan CacheTtl) : IRequest<int>, ICacheableRequest;

    private sealed record PlainQuery : IRequest<int>;

    private sealed class FakeCacheService : ICacheService
    {
        private readonly Dictionary<string, object?> entries = [];

        public async Task<T> GetOrCreateAsync<T>(
            string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken cancellationToken)
        {
            if (entries.TryGetValue(key, out var cached))
                return (T)cached!;

            var value = await factory(cancellationToken);
            entries[key] = value;
            return value;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            entries.Remove(key);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Caches_Response_For_A_Cacheable_Request()
    {
        var behavior = new CachingBehavior<CacheableQuery, int>(new FakeCacheService());
        var request = new CacheableQuery("referrals:v1", TimeSpan.FromMinutes(5));
        var handlerCalls = 0;
        RequestHandlerDelegate<int> next = _ => { handlerCalls++; return Task.FromResult(7); };

        var first = await behavior.Handle(request, next, TestContext.Current.CancellationToken);
        var second = await behavior.Handle(request, next, TestContext.Current.CancellationToken);

        Assert.Equal(7, first);
        Assert.Equal(7, second);
        Assert.Equal(1, handlerCalls); // second served from cache
    }

    [Fact]
    public async Task Passes_Through_A_Non_Cacheable_Request_Every_Time()
    {
        var behavior = new CachingBehavior<PlainQuery, int>(new FakeCacheService());
        var handlerCalls = 0;
        RequestHandlerDelegate<int> next = _ => { handlerCalls++; return Task.FromResult(handlerCalls); };

        var first = await behavior.Handle(new PlainQuery(), next, TestContext.Current.CancellationToken);
        var second = await behavior.Handle(new PlainQuery(), next, TestContext.Current.CancellationToken);

        Assert.Equal(1, first);
        Assert.Equal(2, second); // handler runs each time, nothing cached
        Assert.Equal(2, handlerCalls);
    }
}
