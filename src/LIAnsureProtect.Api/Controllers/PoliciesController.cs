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
    public async Task<ActionResult<ListPoliciesResult>> List(CancellationToken cancellationToken)
    {
        return Ok(await sender.Send(new ListPoliciesQuery(), cancellationToken));
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
