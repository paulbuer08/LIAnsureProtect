using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LIAnsureProtect.Application.Common.Idempotency;
using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Application.Commands.DecideClaim;
using LIAnsureProtect.Modules.Claims.Application.Commands.ManageClaimAdjudication;
using LIAnsureProtect.Modules.Claims.Domain;
using LIAnsureProtect.Platform.Abstractions.Security;
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
public sealed class ClaimsAdjudicationController(
    ISender sender,
    IIdempotencyService idempotencyService,
    ICurrentUser currentUser) : ControllerBase
{
    private const string IdempotencyKeyHeaderName = "Idempotency-Key";
    private static readonly JsonSerializerOptions FingerprintJsonOptions = JsonSerializerOptions.Web;

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

    [HttpPost("{claimId:guid}/accept")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ClaimDecisionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaimDecisionResult>> Accept(
        Guid claimId,
        AcceptClaimRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AcceptClaimCommand(claimId, request.SettlementAmount, request.Reason, request.Notes);

        var idempotencyKey = GetIdempotencyKey();
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var executionResult = await idempotencyService.ExecuteAsync(
                new IdempotencyRequest(
                    idempotencyKey,
                    GetRequiredCurrentUserId(),
                    ApplicationPolicies.AdjudicateClaim,
                    CreateRequestFingerprint("/api/v1/claims/adjudication/{claimId}/accept", new { claimId, request })),
                operationCancellationToken => ExecuteDecisionForIdempotencyAsync(
                    command, "Claim cannot be accepted.", operationCancellationToken),
                cancellationToken);

            return ToIdempotentActionResult<ClaimDecisionResult>(executionResult);
        }

        return await ExecuteAsync<ClaimDecisionResult>(
            () => sender.Send(command, cancellationToken),
            "Claim cannot be accepted.");
    }

    [HttpPost("{claimId:guid}/deny")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ClaimDecisionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaimDecisionResult>> Deny(
        Guid claimId,
        DenyClaimRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ClaimDenialReason>(request.ReasonCategory, ignoreCase: true, out var denialReason))
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Denial reason category is invalid."));

        var command = new DenyClaimCommand(claimId, denialReason, request.Narrative);

        var idempotencyKey = GetIdempotencyKey();
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var executionResult = await idempotencyService.ExecuteAsync(
                new IdempotencyRequest(
                    idempotencyKey,
                    GetRequiredCurrentUserId(),
                    ApplicationPolicies.AdjudicateClaim,
                    CreateRequestFingerprint("/api/v1/claims/adjudication/{claimId}/deny", new { claimId, request })),
                operationCancellationToken => ExecuteDecisionForIdempotencyAsync(
                    command, "Claim cannot be denied.", operationCancellationToken),
                cancellationToken);

            return ToIdempotentActionResult<ClaimDecisionResult>(executionResult);
        }

        return await ExecuteAsync<ClaimDecisionResult>(
            () => sender.Send(command, cancellationToken),
            "Claim cannot be denied.");
    }

    [HttpPost("{claimId:guid}/close")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ClaimDecisionResult>(StatusCodes.Status200OK)]
    public Task<ActionResult<ClaimDecisionResult>> Close(
        Guid claimId,
        CancellationToken cancellationToken)
        => ExecuteAsync<ClaimDecisionResult>(
            () => sender.Send(new CloseClaimCommand(claimId), cancellationToken),
            "Claim cannot be closed.");

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

    private async Task<IdempotencyActionResponse> ExecuteDecisionForIdempotencyAsync<TCommand>(
        TCommand command,
        string conflictTitle,
        CancellationToken cancellationToken)
        where TCommand : MediatR.IRequest<ClaimDecisionResult?>
    {
        try
        {
            var result = await sender.Send(command, cancellationToken);

            return result is null
                ? IdempotencyActionResponse.Json(
                    StatusCodes.Status404NotFound,
                    CreateProblemDetails(StatusCodes.Status404NotFound, "Claim was not found."))
                : IdempotencyActionResponse.Json(StatusCodes.Status200OK, result);
        }
        catch (ArgumentException exception)
        {
            return IdempotencyActionResponse.Json(
                StatusCodes.Status400BadRequest,
                CreateProblemDetails(StatusCodes.Status400BadRequest, "Request is invalid.", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return IdempotencyActionResponse.Json(
                StatusCodes.Status409Conflict,
                CreateProblemDetails(StatusCodes.Status409Conflict, conflictTitle, exception.Message));
        }
    }

    private ActionResult<T> ToIdempotentActionResult<T>(IdempotencyExecutionResult result)
    {
        if (result.Status == IdempotencyExecutionStatus.Conflict)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Idempotency key conflict.",
                result.ConflictDetail));
        }

        var response = result.Response
            ?? throw new InvalidOperationException("A completed idempotency result must include a response.");

        return new ContentResult
        {
            StatusCode = response.StatusCode,
            Content = response.Body,
            ContentType = response.ContentType
        };
    }

    private string? GetIdempotencyKey()
    {
        return Request.Headers.TryGetValue(IdempotencyKeyHeaderName, out var headerValues)
            ? headerValues.FirstOrDefault()
            : null;
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to decide a claim.")
            : currentUser.UserId;
    }

    private static string CreateRequestFingerprint(string routeTemplate, object body)
    {
        var fingerprintPayload = JsonSerializer.Serialize(
            new
            {
                httpMethod = HttpMethods.Post,
                routeTemplate,
                body
            },
            FingerprintJsonOptions);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintPayload));

        return Convert.ToHexString(hash);
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

public sealed record AcceptClaimRequest(decimal SettlementAmount, string Reason, string? Notes);

public sealed record DenyClaimRequest(string ReasonCategory, string Narrative);
