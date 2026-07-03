using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LIAnsureProtect.Application.Common.Idempotency;
using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Modules.Claims.Application;
using LIAnsureProtect.Modules.Claims.Application.Commands.FileClaim;
using LIAnsureProtect.Modules.Claims.Application.Queries.GetMyClaimDetail;
using LIAnsureProtect.Modules.Claims.Application.Queries.ListMyClaims;
using LIAnsureProtect.Modules.Claims.Domain;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
[Route("api/v1/claims")]
public sealed class ClaimsController(
    ISender sender,
    IIdempotencyService idempotencyService,
    ICurrentUser currentUser) : ControllerBase
{
    private const string IdempotencyKeyHeaderName = "Idempotency-Key";
    private static readonly JsonSerializerOptions FingerprintJsonOptions = JsonSerializerOptions.Web;

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.FileClaim)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ClaimResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ClaimResult>> File(
        FileClaimRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ClaimIncidentType>(request.IncidentType, ignoreCase: true, out var incidentType))
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Claim incident type is invalid."));

        var command = new FileClaimCommand(
            request.PolicyId,
            incidentType,
            request.IncidentAtUtc,
            request.DiscoveredAtUtc,
            request.Description);

        var idempotencyKey = GetIdempotencyKey();
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var idempotencyRequest = new IdempotencyRequest(
                idempotencyKey,
                GetRequiredCurrentUserId("file a claim"),
                ApplicationPolicies.FileClaim,
                CreateRequestFingerprint(
                    HttpMethods.Post,
                    "/api/v1/claims",
                    new
                    {
                        request
                    }));

            var executionResult = await idempotencyService.ExecuteAsync(
                idempotencyRequest,
                operationCancellationToken => ExecuteFileForIdempotencyAsync(command, operationCancellationToken),
                cancellationToken);

            return ToActionResult<ClaimResult>(executionResult);
        }

        try
        {
            var result = await sender.Send(command, cancellationToken);

            return result is null
                ? NotFound()
                : Created($"/api/v1/claims/{result.ClaimId}", result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Claim is invalid.",
                exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Claim cannot be filed.",
                exception.Message));
        }
    }

    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.ReadClaim)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ListMyClaimsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ListMyClaimsResult>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListMyClaimsQuery(), cancellationToken);

        return Ok(result);
    }

    [HttpGet("{claimId:guid}")]
    [Authorize(Policy = ApplicationPolicies.ReadClaim)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ClaimDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaimDetailResult>> Detail(
        Guid claimId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyClaimDetailQuery(claimId), cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    private async Task<IdempotencyActionResponse> ExecuteFileForIdempotencyAsync(
        FileClaimCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(command, cancellationToken);

            return result is null
                ? IdempotencyActionResponse.Json(
                    StatusCodes.Status404NotFound,
                    CreateProblemDetails(
                        StatusCodes.Status404NotFound,
                        "Policy was not found."))
                : IdempotencyActionResponse.Json(
                    StatusCodes.Status201Created,
                    result,
                    $"/api/v1/claims/{result.ClaimId}");
        }
        catch (ArgumentException exception)
        {
            return IdempotencyActionResponse.Json(
                StatusCodes.Status400BadRequest,
                CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    "Claim is invalid.",
                    exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return IdempotencyActionResponse.Json(
                StatusCodes.Status409Conflict,
                CreateProblemDetails(
                    StatusCodes.Status409Conflict,
                    "Claim cannot be filed.",
                    exception.Message));
        }
    }

    private string? GetIdempotencyKey()
    {
        return Request.Headers.TryGetValue(IdempotencyKeyHeaderName, out var headerValues)
            ? headerValues.FirstOrDefault()
            : null;
    }

    private string GetRequiredCurrentUserId(string actionDescription)
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException($"An authenticated user id is required to {actionDescription}.")
            : currentUser.UserId;
    }

    private static string CreateRequestFingerprint(
        string httpMethod,
        string routeTemplate,
        object body)
    {
        var fingerprintPayload = JsonSerializer.Serialize(
            new
            {
                httpMethod,
                routeTemplate,
                body
            },
            FingerprintJsonOptions);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintPayload));

        return Convert.ToHexString(hash);
    }

    private ActionResult<T> ToActionResult<T>(IdempotencyExecutionResult result)
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

        if (!string.IsNullOrWhiteSpace(response.Location))
            Response.Headers.Location = response.Location;

        return new ContentResult
        {
            StatusCode = response.StatusCode,
            Content = response.Body,
            ContentType = response.ContentType
        };
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

public sealed record FileClaimRequest(
    Guid PolicyId,
    string IncidentType,
    DateTime IncidentAtUtc,
    DateTime DiscoveredAtUtc,
    string Description);
