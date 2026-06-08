using LIAnsureProtect.Application.Submissions.Commands.CreateSubmission;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ApplicationValidationException = LIAnsureProtect.Application.Common.Exceptions.ValidationException;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class SubmissionsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateSubmissionResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
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
