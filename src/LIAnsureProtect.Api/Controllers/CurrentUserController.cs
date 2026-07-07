using LIAnsureProtect.Platform.Abstractions.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIAnsureProtect.Api.Controllers;

/// <summary>
/// The provider-neutral "who am I" endpoint. The SPA calls it to learn the caller's identity and
/// roles from the <b>same</b> source the authorization policies use (<see cref="ICurrentUser"/>),
/// so the UI and the API can never disagree about roles — and the SPA never has to parse a token.
/// When the identity provider changes (e.g. Auth0 → Cognito in M48), this endpoint and the SPA are
/// untouched; only the API's token-validation configuration changes.
/// </summary>
[ApiController]
[Route("api/v1/me")]
[Authorize]
public sealed class CurrentUserController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<CurrentUserResult>(StatusCodes.Status200OK)]
    public ActionResult<CurrentUserResult> Get()
    {
        return Ok(new CurrentUserResult(
            currentUser.UserId ?? string.Empty,
            currentUser.Email,
            currentUser.GetRoles().ToArray()));
    }
}

public sealed record CurrentUserResult(
    string UserId,
    string? Email,
    IReadOnlyCollection<string> Roles);
