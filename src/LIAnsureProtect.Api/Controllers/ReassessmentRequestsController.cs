using System.ComponentModel.DataAnnotations;
using LIAnsureProtect.Application.Common.Exceptions;
using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Quotes.Reassessments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
public sealed class ReassessmentRequestsController(ISender sender) : ControllerBase
{
    [HttpGet("api/v1/submissions/{submissionId:guid}/reassessment-requests")]
    [Authorize(Policy = ApplicationPolicies.ReadSubmission)]
    [ProducesResponseType<IReadOnlyCollection<ReassessmentRequestResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ReassessmentRequestResult>>> ListOwned(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListOwnedReassessmentRequestsQuery(submissionId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("api/v1/underwriting/reassessment-requests")]
    [Authorize(Policy = ApplicationPolicies.UnderwriteQuote)]
    [ProducesResponseType<IReadOnlyCollection<ReassessmentRequestResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ReassessmentRequestResult>>> ListForReview(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(new ListReassessmentRequestsForReviewQuery(status), cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Problem(title: "Reassessment request filters are invalid.", detail: exception.Message));
        }
    }

    [HttpPost("api/v1/underwriting/reassessment-requests/{requestId:guid}/approve")]
    [Authorize(Policy = ApplicationPolicies.UnderwriteQuote)]
    [ProducesResponseType<ReassessmentRequestResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReassessmentRequestResult>> Approve(
        Guid requestId,
        ReviewReassessmentRequest request,
        CancellationToken cancellationToken)
        => await ExecuteReviewAsync(new ApproveReassessmentRequestCommand(requestId, request.Reason), cancellationToken);

    [HttpPost("api/v1/underwriting/reassessment-requests/{requestId:guid}/decline")]
    [Authorize(Policy = ApplicationPolicies.UnderwriteQuote)]
    [ProducesResponseType<ReassessmentRequestResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReassessmentRequestResult>> Decline(
        Guid requestId,
        ReviewReassessmentRequest request,
        CancellationToken cancellationToken)
        => await ExecuteReviewAsync(new DeclineReassessmentRequestCommand(requestId, request.Reason), cancellationToken);

    private async Task<ActionResult<ReassessmentRequestResult>> ExecuteReviewAsync(
        IRequest<ReassessmentRequestResult?> command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(command, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (BusinessConflictException exception)
        {
            return Conflict(Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Reassessment request cannot be reviewed.",
                detail: exception.PublicMessage,
                extensions: new Dictionary<string, object?> { ["code"] = exception.Code }));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Reassessment request cannot be reviewed.",
                detail: exception.Message));
        }
    }
}

public sealed record ReviewReassessmentRequest(
    [Required, MinLength(3), MaxLength(2000)] string Reason);
