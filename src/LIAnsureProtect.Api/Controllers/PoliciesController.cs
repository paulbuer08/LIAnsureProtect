using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Policies.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = ApplicationPolicies.ReadPolicy)]
public sealed class PoliciesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ListPoliciesResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ListPoliciesResult>> List(
        [FromQuery] string? search,
        [FromQuery] string? contractualStatus,
        [FromQuery] string? coverageState,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(
                new ListPoliciesQuery(search, contractualStatus, coverageState), cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Policy filters are invalid.", Detail = exception.Message });
        }
    }

    [HttpGet("{policyId:guid}")]
    [ProducesResponseType<PolicyResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PolicyResult>> GetById(
        Guid policyId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPolicyQuery(policyId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
