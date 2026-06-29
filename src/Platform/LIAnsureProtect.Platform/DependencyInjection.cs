using LIAnsureProtect.Platform.Abstractions;
using LIAnsureProtect.Platform.Abstractions.Time;
using LIAnsureProtect.Platform.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.Platform;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the shared-kernel platform services and resolves the active deployment profile.
    /// Call this once from each host's composition root, before the layer/module registrations
    /// that branch on the profile.
    /// </summary>
    public static IServiceCollection AddPlatform(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var profile = PlatformProfileResolver.Resolve(configuration);

        // Make the resolved profile available to anything that wants to read it via DI
        // (resolve PlatformOptions and read .Profile).
        services.AddSingleton(new PlatformOptions { Profile = profile });

        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
