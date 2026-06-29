using LIAnsureProtect.Platform.Abstractions;
using Microsoft.Extensions.Configuration;

namespace LIAnsureProtect.Platform;

/// <summary>
/// Single source of truth for reading the <c>Platform:Profile</c> configuration value.
/// Used by both <see cref="DependencyInjection.AddPlatform"/> and the hosts' composition roots
/// so the Local &#8644; AWS switch is interpreted identically everywhere.
/// </summary>
public static class PlatformProfileResolver
{
    /// <summary>
    /// Reads <c>Platform:Profile</c>. Missing/empty defaults to <see cref="PlatformProfile.Local"/>.
    /// An unrecognized value fails fast so a typo never silently runs the wrong adapters.
    /// </summary>
    public static PlatformProfile Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var raw = configuration[$"{PlatformOptions.SectionName}:{nameof(PlatformOptions.Profile)}"];

        if (string.IsNullOrWhiteSpace(raw))
        {
            return PlatformProfile.Local;
        }

        if (Enum.TryParse<PlatformProfile>(raw, ignoreCase: true, out var profile) && Enum.IsDefined(profile))
        {
            return profile;
        }

        throw new InvalidOperationException(
            $"Configuration '{PlatformOptions.SectionName}:{nameof(PlatformOptions.Profile)}' has unsupported value '{raw}'. " +
            $"Valid values are '{nameof(PlatformProfile.Local)}' or '{nameof(PlatformProfile.Aws)}'.");
    }
}
