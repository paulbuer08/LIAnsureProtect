namespace LIAnsureProtect.Infrastructure.Caching;

/// <summary>
/// Cache configuration bound from the <c>Cache</c> section. The Redis settings are used under
/// <c>Platform:Profile=Aws</c>.
/// </summary>
public sealed class CacheOptions
{
    /// <summary>Redis connection string (e.g. <c>localhost:6379</c>). Required under the Aws profile.</summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>Key prefix so multiple apps can share one Redis without collisions.</summary>
    public string? KeyPrefix { get; set; }
}
