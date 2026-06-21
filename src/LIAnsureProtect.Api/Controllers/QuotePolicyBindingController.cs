using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LIAnsureProtect.Application.Common.Idempotency;
using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Policies.Commands.BindPolicy;
using LIAnsureProtect.Application.Quotes.Commands.AcceptQuote;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApplicationValidationException = LIAnsureProtect.Application.Common.Exceptions.ValidationException;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
[Route("api/v1/quotes/{quoteId:guid}")]
public sealed class QuotePolicyBindingController(
    ISender sender,
    IIdempotencyService idempotencyService,
    ICurrentUser currentUser) : ControllerBase
{
    private const string IdempotencyKeyHeaderName = "Idempotency-Key";
    private static readonly JsonSerializerOptions FingerprintJsonOptions = JsonSerializerOptions.Web;

    [HttpPost("accept")]
    [Authorize(Policy = ApplicationPolicies.AcceptQuote)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AcceptQuoteResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AcceptQuoteResult>> Accept(
        Guid quoteId,
        AcceptQuoteRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AcceptQuoteCommand(
            quoteId,
            request.AcceptedByName,
            request.AcceptedByTitle,
            request.SubjectivitiesAcknowledged);
        var idempotencyKey = GetIdempotencyKey();
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var idempotencyRequest = new IdempotencyRequest(
                idempotencyKey,
                GetRequiredCurrentUserId("accept a quote"),
                ApplicationPolicies.AcceptQuote,
                CreateRequestFingerprint(
                    HttpMethods.Post,
                    "/api/v1/quotes/{quoteId}/accept",
                    new
                    {
                        quoteId,
                        request
                    }));

            var executionResult = await idempotencyService.ExecuteAsync(
                idempotencyRequest,
                operationCancellationToken => ExecuteAcceptForIdempotencyAsync(command, operationCancellationToken),
                cancellationToken);

            return ToActionResult<AcceptQuoteResult>(executionResult);
        }

        try
        {
            var result = await sender.Send(command, cancellationToken);

            return result is null
                ? NotFound()
                : Ok(result);
        }
        catch (ApplicationValidationException exception)
        {
            return BadRequest(CreateValidationProblemDetails(exception));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Quote cannot be accepted.",
                exception.Message));
        }
    }

    [HttpPost("bind")]
    [Authorize(Policy = ApplicationPolicies.BindPolicy)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<BindPolicyResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BindPolicyResult>> Bind(
        Guid quoteId,
        BindPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var command = new BindPolicyCommand(
            quoteId,
            request.EffectiveDateUtc);
        var idempotencyKey = GetIdempotencyKey();
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var idempotencyRequest = new IdempotencyRequest(
                idempotencyKey,
                GetRequiredCurrentUserId("bind a policy"),
                ApplicationPolicies.BindPolicy,
                CreateRequestFingerprint(
                    HttpMethods.Post,
                    "/api/v1/quotes/{quoteId}/bind",
                    new
                    {
                        quoteId,
                        request
                    }));

            var executionResult = await idempotencyService.ExecuteAsync(
                idempotencyRequest,
                operationCancellationToken => ExecuteBindForIdempotencyAsync(command, operationCancellationToken),
                cancellationToken);

            return ToActionResult<BindPolicyResult>(executionResult);
        }

        try
        {
            var result = await sender.Send(command, cancellationToken);

            return result is null
                ? NotFound()
                : Created($"/api/v1/policies/{result.PolicyId}", result);
        }
        catch (ApplicationValidationException exception)
        {
            return BadRequest(CreateValidationProblemDetails(exception));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Policy cannot be bound.",
                exception.Message));
        }
    }

    private async Task<IdempotencyActionResponse> ExecuteAcceptForIdempotencyAsync(
        AcceptQuoteCommand command,
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
                        "Quote was not found."))
                : IdempotencyActionResponse.Json(
                    StatusCodes.Status200OK,
                    result);
        }
        catch (ApplicationValidationException exception)
        {
            return IdempotencyActionResponse.Json(
                StatusCodes.Status400BadRequest,
                CreateValidationProblemDetails(exception));
        }
        catch (InvalidOperationException exception)
        {
            return IdempotencyActionResponse.Json(
                StatusCodes.Status409Conflict,
                CreateProblemDetails(
                    StatusCodes.Status409Conflict,
                    "Quote cannot be accepted.",
                    exception.Message));
        }
    }

    private async Task<IdempotencyActionResponse> ExecuteBindForIdempotencyAsync(
        BindPolicyCommand command,
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
                        "Quote was not found."))
                : IdempotencyActionResponse.Json(
                    StatusCodes.Status201Created,
                    result,
                    $"/api/v1/policies/{result.PolicyId}");
        }
        catch (ApplicationValidationException exception)
        {
            return IdempotencyActionResponse.Json(
                StatusCodes.Status400BadRequest,
                CreateValidationProblemDetails(exception));
        }
        catch (InvalidOperationException exception)
        {
            return IdempotencyActionResponse.Json(
                StatusCodes.Status409Conflict,
                CreateProblemDetails(
                    StatusCodes.Status409Conflict,
                    "Policy cannot be bound.",
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

    private static HttpValidationProblemDetails CreateValidationProblemDetails(
        ApplicationValidationException exception)
    {
        return new HttpValidationProblemDetails(
            exception.Errors.ToDictionary(
                error => error.Key,
                error => error.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };
    }
}

public sealed record AcceptQuoteRequest(
    string AcceptedByName,
    string AcceptedByTitle,
    bool SubjectivitiesAcknowledged);

public sealed record BindPolicyRequest(DateTime EffectiveDateUtc);
