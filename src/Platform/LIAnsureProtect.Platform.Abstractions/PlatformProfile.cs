namespace LIAnsureProtect.Platform.Abstractions;

/// <summary>
/// Selects which set of infrastructure adapters the composition root wires up.
/// This is the "Local &#8644; AWS deploy switch": the same code runs everywhere; only the
/// adapters behind the ports change. Driven by the <c>Platform:Profile</c> configuration value.
/// </summary>
public enum PlatformProfile
{
    /// <summary>Local/developer adapters (filesystem storage, in-process messaging, Postgres in Docker).</summary>
    Local = 0,

    /// <summary>AWS adapters (S3, SNS/SQS, Aurora, ...). Introduced incrementally in later milestones.</summary>
    Aws = 1
}
