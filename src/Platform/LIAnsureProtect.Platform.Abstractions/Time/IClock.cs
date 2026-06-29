namespace LIAnsureProtect.Platform.Abstractions.Time;

/// <summary>
/// Abstraction over the system clock so domain/application code can read "now"
/// without binding to <see cref="DateTime.UtcNow"/> directly. Makes time-dependent
/// behavior testable and is a natural shared-kernel primitive every module reuses.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
