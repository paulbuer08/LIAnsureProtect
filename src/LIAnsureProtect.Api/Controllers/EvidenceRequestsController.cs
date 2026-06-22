using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Quotes.Commands.ManageQuoteEvidenceRequests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
[Route("api/v1/evidence-requests")]
[Authorize(Policy = ApplicationPolicies.RespondToEvidenceRequest)]
public sealed class EvidenceRequestsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ListOwnerEvidenceRequestsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ListOwnerEvidenceRequestsResult>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListOwnerEvidenceRequestsQuery(), cancellationToken);

        return Ok(result);
    }

    [HttpPost("{evidenceRequestId:guid}/respond")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<QuoteEvidenceRequestResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteEvidenceRequestResult>> Respond(
        Guid evidenceRequestId,
        RespondToEvidenceRequestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new RespondToQuoteEvidenceRequestCommand(
                    evidenceRequestId,
                    request.RespondentName,
                    request.RespondentTitle,
                    request.ResponseText,
                    request.AttachmentFileName,
                    request.AttachmentContentType,
                    request.AttachmentSizeBytes),
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Evidence response is invalid.",
                exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Evidence request cannot be updated.",
                exception.Message));
        }
    }

    private static ProblemDetails CreateProblemDetails(
        int status,
        string title,
        string? detail = null)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }
}

public sealed record RespondToEvidenceRequestRequest(
    string RespondentName,
    string RespondentTitle,
    string ResponseText,
    string? AttachmentFileName,
    string? AttachmentContentType,
    long? AttachmentSizeBytes);
