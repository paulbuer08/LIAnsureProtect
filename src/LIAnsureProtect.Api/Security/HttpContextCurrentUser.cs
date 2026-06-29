using LIAnsureProtect.Platform.Abstractions.Security;
using System.Security.Claims;

namespace LIAnsureProtect.Api.Security;


public sealed class HttpContextCurrentUser(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration)
        : ICurrentUser
{
    private ClaimsPrincipal? User =>
        httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated == true;

    public string? UserId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User?.FindFirstValue("sub");

    public string? Email =>
        User?.FindFirstValue(ClaimTypes.Email)
        ?? User?.FindFirstValue("email");


    public IReadOnlyCollection<string> GetRoles()
    {
        // Read roles using the identity's own RoleClaimType so GetRoles() always agrees with
        // IsInRole(). The JWT bearer sets this to Authentication:RoleClaimType in production; the
        // config fallback keeps behavior if no authenticated identity is present.
        var roleClaimType = (User?.Identity as ClaimsIdentity)?.RoleClaimType
            ?? configuration["Authentication:RoleClaimType"]
            ?? ClaimTypes.Role;
        return User?.FindAll(roleClaimType)
            .Select(claim => claim.Value)
            .ToArray()
        ?? [];
    }

    public bool IsInRole(string role) =>
        User?.IsInRole(role) == true;

}
