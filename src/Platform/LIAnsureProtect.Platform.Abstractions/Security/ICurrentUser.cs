namespace LIAnsureProtect.Platform.Abstractions.Security;

/// <summary>
/// The caller's identity, exposed to Application use cases without binding them to ASP.NET Core.
/// A shared-kernel port so every module can ask "who is calling?" the same way.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? Email { get; }
    IReadOnlyCollection<string> GetRoles();
    bool IsInRole(string role);
}
