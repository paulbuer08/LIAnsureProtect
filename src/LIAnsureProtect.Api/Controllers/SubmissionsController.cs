using LIAnsureProtect.Application.Submissions.Commands.CreateSubmission;
using LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;
using LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;
using LIAnsureProtect.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using ApplicationValidationException = LIAnsureProtect.Application.Common.Exceptions.ValidationException;

namespace LIAnsureProtect.Api.Controllers;


[ApiController]
[Route("api/v1/[controller]")]
public sealed class SubmissionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CreateSubmission)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ListSubmissionsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ListSubmissionsResult>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListSubmissionsQuery(), cancellationToken);

        return Ok(result);
    }

    [HttpGet("{submissionId:guid}")]
    [Authorize(Policy = ApplicationPolicies.CreateSubmission)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<SubmissionDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SubmissionDetailResult>> GetById(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSubmissionDetailQuery(submissionId),
            cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CreateSubmission)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<CreateSubmissionResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateSubmissionResult>> Create(
        CreateSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateSubmissionCommand(
            request.ApplicantName,
            request.ApplicantEmail,
            request.CompanyName);

        try
        {
            var result = await sender.Send(command, cancellationToken);

            return Created($"/api/v1/submissions/{result.SubmissionId}", result);
        }
        catch (ApplicationValidationException exception)
        {
            var problemDetails = new HttpValidationProblemDetails(
                exception.Errors.ToDictionary(
                    error => error.Key,
                    error => error.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred."
            };

            return BadRequest(problemDetails);
        }
    }

}



public sealed record CreateSubmissionRequest(
    string ApplicantName,
    string ApplicantEmail,
    string CompanyName);
