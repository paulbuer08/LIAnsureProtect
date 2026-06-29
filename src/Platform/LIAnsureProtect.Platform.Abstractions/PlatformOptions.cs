namespace LIAnsureProtect.Platform.Abstractions;

/// <summary>
/// Strongly-typed view of the <c>Platform</c> configuration section.
/// </summary>
public sealed class PlatformOptions
{
    /// <summary>Configuration section name: <c>Platform</c>.</summary>
    public const string SectionName = "Platform";

    /// <summary>Which adapter set to compose. Defaults to <see cref="PlatformProfile.Local"/>.</summary>
    public PlatformProfile Profile { get; set; } = PlatformProfile.Local;
}
