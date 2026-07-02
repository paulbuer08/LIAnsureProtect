using LIAnsureProtect.Infrastructure.Caching;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;

namespace LIAnsureProtect.IntegrationTests.Caching;

/// <summary>
/// Opt-in round-trip test proving the Redis cache adapter really stores, re-reads, and evicts
/// values against a live Redis — no AWS account (local Docker Redis). Skipped by default (like the
/// S3/SNS opt-ins) so the standard test/CI path stays green; enable with
/// <c>LIANSUREPROTECT_RUN_REDIS_TESTS=true</c> after starting Redis
/// (<c>docker compose --profile aws-local up -d</c>).
/// </summary>
public sealed class RedisCacheServiceTests
{
    private const string EnabledEnvironmentVariableName = "LIANSUREPROTECT_RUN_REDIS_TESTS";
    private const string ConnectionEnvironmentVariableName = "LIANSUREPROTECT_TEST_REDIS_CONNECTION";
    private const string DefaultConnection = "localhost:6379";

    [Fact]
    public async Task Stores_Reads_Back_And_Evicts_Through_Redis()
    {
        Assert.SkipUnless(
            RedisTestsAreEnabled(),
            $"Set {EnabledEnvironmentVariableName}=true (and start Redis) to run Redis-backed cache tests.");

        var cancellationToken = TestContext.Current.CancellationToken;
        using var redisCache = new RedisCache(Options.Create(new RedisCacheOptions
        {
            Configuration = GetConnection()
        }));
        var service = new RedisCacheService(
            redisCache,
            Options.Create(new CacheOptions { KeyPrefix = $"liap-test:{Guid.NewGuid():N}:" }));

        var key = "referrals:summary";
        var factoryCalls = 0;

        var first = await service.GetOrCreateAsync(
            key, _ => { factoryCalls++; return Task.FromResult(42); },
            TimeSpan.FromMinutes(1), cancellationToken);
        var second = await service.GetOrCreateAsync(
            key, _ => { factoryCalls++; return Task.FromResult(99); },
            TimeSpan.FromMinutes(1), cancellationToken);

        Assert.Equal(42, first);
        Assert.Equal(42, second); // served from Redis, not rebuilt
        Assert.Equal(1, factoryCalls);

        await service.RemoveAsync(key, cancellationToken);

        var afterEviction = await service.GetOrCreateAsync(
            key, _ => { factoryCalls++; return Task.FromResult(7); },
            TimeSpan.FromMinutes(1), cancellationToken);

        Assert.Equal(7, afterEviction);
        Assert.Equal(2, factoryCalls);
    }

    private static bool RedisTestsAreEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(EnabledEnvironmentVariableName),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetConnection()
    {
        return Environment.GetEnvironmentVariable(ConnectionEnvironmentVariableName) ?? DefaultConnection;
    }
}
