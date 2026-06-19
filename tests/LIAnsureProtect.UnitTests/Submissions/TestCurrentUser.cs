using LIAnsureProtect.Application.Common.Security;

namespace LIAnsureProtect.UnitTests.Submissions;

internal sealed class TestCurrentUser(
    string? userId = "test-user-1",
    string? email = "test-user@example.com",
    IReadOnlyCollection<string>? roles = null)
    : ICurrentUser
{
    private readonly IReadOnlyCollection<string> roles = roles ?? [];

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);

    public string? UserId { get; } = userId;

    public string? Email { get; } = email;

    public IReadOnlyCollection<string> GetRoles() => roles;

    public bool IsInRole(string role) => roles.Contains(role);
}
