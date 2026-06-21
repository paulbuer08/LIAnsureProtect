using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LIAnsureProtect.Application.Common.Exceptions;
using LIAnsureProtect.Application.Common.Idempotency;
using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Quotes.Commands.CreateQuote;
using LIAnsureProtect.Domain.Quotes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApplicationValidationException = LIAnsureProtect.Application.Common.Exceptions.ValidationException;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
[Route("api/v1/submissions/{submissionId:guid}/quotes")]
public sealed class SubmissionQuotesController(
    ISender sender,
    IIdempotencyService idempotencyService,
    ICurrentUser currentUser) : ControllerBase
{
    private const string IdempotencyKeyHeaderName = "Idempotency-Key";
    private static readonly JsonSerializerOptions FingerprintJsonOptions = JsonSerializerOptions.Web;

    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CreateQuote)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<CreateQuoteResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateQuoteResult>> Create(
        Guid submissionId,
        CreateQuoteRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateQuoteCommand(
            submissionId,
            request.IndustryClass,
            request.AnnualRevenueBand,
            request.RequestedLimit,
            request.Retention,
            request.MfaStatus,
            request.EdrStatus,
            request.BackupMaturity,
            request.HasIncidentResponsePlan,
            request.PriorCyberIncidents,
            request.SensitiveDataExposure);

        var idempotencyKey = GetIdempotencyKey();
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var idempotencyRequest = new IdempotencyRequest(
                idempotencyKey,
                GetRequiredCurrentUserId(),
                ApplicationPolicies.CreateQuote,
                CreateRequestFingerprint(
                    HttpMethods.Post,
                    "/api/v1/submissions/{submissionId}/quotes",
                    new
                    {
                        submissionId,
                        request
                    }));

            var executionResult = await idempotencyService.ExecuteAsync(
                idempotencyRequest,
                async operationCancellationToken =>
                {
                    try
                    {
                        var result = await sender.Send(command, operationCancellationToken);

                        return result is null
                            ? IdempotencyActionResponse.Json(
                                StatusCodes.Status404NotFound,
                                CreateProblemDetails(
                                    StatusCodes.Status404NotFound,
                                    "Submission was not found."))
                            : IdempotencyActionResponse.Json(
                                StatusCodes.Status201Created,
                                result,
                                $"/api/v1/quotes/{result.QuoteId}");
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
                                "Quote cannot be created.",
                                exception.Message));
                    }
                },
                cancellationToken);

            return ToActionResult<CreateQuoteResult>(executionResult);
        }

        try
        {
            var result = await sender.Send(command, cancellationToken);

            return result is null
                ? NotFound()
                : Created($"/api/v1/quotes/{result.QuoteId}", result);
        }
        catch (ApplicationValidationException exception)
        {
            return BadRequest(CreateValidationProblemDetails(exception));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Quote cannot be created.",
                exception.Message));
        }
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
            ? throw new InvalidOperationException("An authenticated user id is required to create a quote.")
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

public sealed record CreateQuoteRequest(
    CyberIndustryClass IndustryClass,
    AnnualRevenueBand AnnualRevenueBand,
    decimal RequestedLimit,
    decimal Retention,
    CyberSecurityControlStatus MfaStatus,
    CyberSecurityControlStatus EdrStatus,
    BackupMaturity BackupMaturity,
    bool HasIncidentResponsePlan,
    int PriorCyberIncidents,
    SensitiveDataExposure SensitiveDataExposure);
