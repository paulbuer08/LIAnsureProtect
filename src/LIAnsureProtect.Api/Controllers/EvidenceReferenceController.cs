using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.GetEvidenceReferenceData;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIAnsureProtect.Api.Controllers;

/// <summary>
/// Non-sensitive evidence reference data (categories + upload rules) for any signed-in role:
/// owners need the upload rules, underwriters need the category list. The backing query is the
/// first production cache-aside adoption — served from ICacheService after the first call.
/// </summary>
[ApiController]
[Route("api/v1/evidence-requests/reference")]
[Authorize]
public sealed class EvidenceReferenceController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<EvidenceReferenceDataResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvidenceReferenceDataResult>> Get(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetEvidenceReferenceDataQuery(), cancellationToken);

        return Ok(result);
    }
}
