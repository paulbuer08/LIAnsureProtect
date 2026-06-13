namespace LIAnsureProtect.Application.Common.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? Email { get; }
    IReadOnlyCollection<string> GetRoles();
    bool IsInRole(string role);
}
