using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace LIAnsureProtect.IntegrationTests.Security;


public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "Test";
    public const string UserIdHeader = "X-Test-UserId";
    public const string EmailHeader = "X-Test-Email";
    public const string RolesHeader = "X-Test-Roles";


    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userIdValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = userIdValues.ToString();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var email = Request.Headers.TryGetValue(EmailHeader, out var emailValues)
            ? emailValues.ToString()
            : "test-user@example.com";

        var roles = Request.Headers.TryGetValue(RolesHeader, out var roleValues)
            ? roleValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
            new(ClaimTypes.Email, email),
            new("email", email)
        };

        foreach (var role in roles)
        {
            claims.Add(new("roles", role));
        }

        var identity = new ClaimsIdentity(
            claims,
            AuthenticationScheme,
            ClaimTypes.Name,
            "roles");

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

}
