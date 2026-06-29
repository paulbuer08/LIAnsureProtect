using LIAnsureProtect.Platform.Abstractions.Time;

namespace LIAnsureProtect.Platform.Time;

/// <summary>
/// Default <see cref="IClock"/> adapter that reads the real system clock in UTC.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
