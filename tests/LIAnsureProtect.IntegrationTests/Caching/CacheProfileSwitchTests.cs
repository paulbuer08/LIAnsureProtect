using LIAnsureProtect.Infrastructure;
using LIAnsureProtect.Infrastructure.Caching;
using LIAnsureProtect.Platform.Abstractions;
using LIAnsureProtect.Platform.Abstractions.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.IntegrationTests.Caching;

/// <summary>
/// Proves the Local ⇄ AWS deploy switch on the cache port: the active <see cref="PlatformProfile"/>
/// selects the adapter (in-memory vs Redis), and an Aws profile with no Redis connection string
/// fails fast rather than silently wiring a connectionless cache.
/// </summary>
public sealed class CacheProfileSwitchTests
{
    private const string TestConnectionString =
        "Host=localhost;Database=liansureprotect_test;Username=postgres;Password=postgres";

    [Fact]
    public void LocalProfileWiresTheInMemoryCacheAdapter()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(TestConnectionString, PlatformProfile.Local);

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<ICacheService>();

        Assert.IsType<InMemoryCacheService>(cache);
    }

    [Fact]
    public void AwsProfileWiresTheRedisCacheAdapter()
    {
        var services = new ServiceCollection();
        services.Configure<CacheOptions>(options => options.RedisConnectionString = "localhost:6379");
        services.AddInfrastructure(TestConnectionString, PlatformProfile.Aws);

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<ICacheService>();

        Assert.IsType<RedisCacheService>(cache);
    }

    [Fact]
    public void AwsProfileFailsFastWhenRedisConnectionMissing()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(TestConnectionString, PlatformProfile.Aws);

        using var provider = services.BuildServiceProvider();

        // No Cache:RedisConnectionString configured → resolving the cache must fail fast.
        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<ICacheService>());
    }
}
