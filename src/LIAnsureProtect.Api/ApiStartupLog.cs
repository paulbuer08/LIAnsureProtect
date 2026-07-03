namespace LIAnsureProtect.Api;

/// <summary>Source-generated startup log messages for the API host (CA1848).</summary>
internal static partial class ApiStartupLog
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starting {Application} in {Environment} mode.")]
    public static partial void Starting(ILogger logger, string application, string environment);
}
