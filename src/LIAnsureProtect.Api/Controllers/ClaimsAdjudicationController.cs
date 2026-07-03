using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Application.Commands.ManageClaimAdjudication;
using LIAnsureProtect.Modules.Claims.Application.Commands.ManageClaimFinancials;
using LIAnsureProtect.Modules.Claims.Application.Documents;
using LIAnsureProtect.Modules.Claims.Application.Queries.GetClaimForAdjudication;
using LIAnsureProtect.Modules.Claims.Application.Queries.ListClaimsForAdjudication;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIAnsureProtect.Api.Controllers;

/// <summary>
/// The claims adjuster's workbench API: queue, assignment (M44.5 guarded claim — the loser of a
/// race gets 409 and refetches), internal work notes, and information requests to the claimant.
/// </summary>
[ApiController]
[Route("api/v1/claims/adjudication")]
[Authorize(Policy = ApplicationPolicies.AdjudicateClaim)]
public sealed class ClaimsAdjudicationController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ListClaimsForAdjudicationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ListClaimsForAdjudicationResult>> Queue(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListClaimsForAdjudicationQuery(), cancellationToken);

        return Ok(result);
    }

    [HttpGet("{claimId:guid}")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ClaimAdjudicationDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaimAdjudicationDetailResult>> Detail(
        Guid claimId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetClaimForAdjudicationQuery(claimId), cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [HttpPost("{claimId:guid}/assign-to-me")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ClaimAdjudicationResult>(StatusCodes.Status200OK)]
    public Task<ActionResult<ClaimAdjudicationResult>> AssignToMe(
        Guid claimId,
        CancellationToken cancellationToken)
        => ExecuteAsync<ClaimAdjudicationResult>(
            () => sender.Send(new AssignClaimToMeCommand(claimId), cancellationToken),
            "Claim cannot be assigned.");

    [HttpPost("{claimId:guid}/release-assignment")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ClaimAdjudicationResult>(StatusCodes.Status200OK)]
    public Task<ActionResult<ClaimAdjudicationResult>> ReleaseAssignment(
        Guid claimId,
        CancellationToken cancellationToken)
        => ExecuteAsync<ClaimAdjudicationResult>(
            () => sender.Send(new ReleaseClaimAssignmentCommand(claimId), cancellationToken),
            "Claim assignment cannot be released.");

    [HttpPost("{claimId:guid}/notes")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ClaimWorkNoteResult>(StatusCodes.Status201Created)]
    public Task<ActionResult<ClaimWorkNoteResult>> AddNote(
        Guid claimId,
        AddClaimWorkNoteRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync<ClaimWorkNoteResult>(
            () => sender.Send(new AddClaimWorkNoteCommand(claimId, request.Note), cancellationToken),
            "Work note cannot be added.",
            created: true);

    [HttpPost("{claimId:guid}/information-requests")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ClaimInformationRequestResult>(StatusCodes.Status201Created)]
    public Task<ActionResult<ClaimInformationRequestResult>> RequestInformation(
        Guid claimId,
        RequestClaimInformationRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync<ClaimInformationRequestResult>(
            () => sender.Send(
                new RequestClaimInformationCommand(claimId, request.Title, request.Message),
                cancellationToken),
            "Information cannot be requested.",
            created: true);

    [HttpPost("{claimId:guid}/reserve")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ClaimFinancialsResult>(StatusCodes.Status200OK)]
    public Task<ActionResult<ClaimFinancialsResult>> SetReserve(
        Guid claimId,
        SetClaimReserveRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync<ClaimFinancialsResult>(
            () => sender.Send(new SetClaimReserveCommand(claimId, request.Amount, request.Reason), cancellationToken),
            "Reserve cannot be changed.");

    [HttpGet("{claimId:guid}/documents/{documentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadDocument(
        Guid claimId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new DownloadAdjudicationClaimDocumentQuery(claimId, documentId),
                cancellationToken);

            return result is null
                ? NotFound()
                : File(result.Content, result.ContentType, result.OriginalFileName);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Claim document cannot be downloaded.",
                exception.Message));
        }
    }

    private async Task<ActionResult<T>> ExecuteAsync<T>(
        Func<Task<T?>> action,
        string conflictTitle,
        bool created = false)
        where T : class
    {
        try
        {
            var result = await action();

            if (result is null)
                return NotFound();

            return created
                ? Created(Request.Path, result)
                : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Request is invalid.",
                exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                conflictTitle,
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

public sealed record AddClaimWorkNoteRequest(string Note);

public sealed record RequestClaimInformationRequest(string Title, string Message);

public sealed record SetClaimReserveRequest(decimal Amount, string Reason);
