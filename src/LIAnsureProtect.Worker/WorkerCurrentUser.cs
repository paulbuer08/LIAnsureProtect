using LIAnsureProtect.Platform.Abstractions.Security;

namespace LIAnsureProtect.Worker;

/// <summary>
/// Background jobs do not run inside an HTTP request, but the composed application handlers still
/// need an identity service during dependency validation.
/// </summary>
public sealed class WorkerCurrentUser : ICurrentUser
{
    private const string SystemUserId = "system:worker";

    public bool IsAuthenticated => true;

    public string? UserId => SystemUserId;

    public string? Email => "worker@liansureprotect.local";

    public IReadOnlyCollection<string> GetRoles() => [];

    public bool IsInRole(string role) => false;
}
