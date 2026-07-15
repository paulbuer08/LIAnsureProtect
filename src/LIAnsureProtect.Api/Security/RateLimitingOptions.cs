namespace LIAnsureProtect.Api.Security;

/// <summary>
/// Rate-limit settings bound from the <c>RateLimiting</c> configuration section. Defaults are
/// generous so only genuine floods are limited; production tightens them via configuration/env.
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>Permitted safe (read) requests per window, per caller.</summary>
    public int SafePermitLimit { get; set; } = 5000;

    /// <summary>Permitted unsafe (write) requests per window, per caller.</summary>
    public int UnsafePermitLimit { get; set; } = 2000;

    /// <summary>Length of the fixed window, in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Permitted draft-creation attempts per caller in the dedicated draft window.</summary>
    public int DraftCreatePermitLimit { get; set; } = 20;

    /// <summary>Length of the dedicated draft-creation window, in seconds.</summary>
    public int DraftCreateWindowSeconds { get; set; } = 60;

    /// <summary>Permitted quote/reassessment creation attempts per caller and submission.</summary>
    public int ReassessmentPermitLimit { get; set; } = 3;

    /// <summary>Length of the quote/reassessment burst-protection window, in seconds.</summary>
    public int ReassessmentWindowSeconds { get; set; } = 600;
}
