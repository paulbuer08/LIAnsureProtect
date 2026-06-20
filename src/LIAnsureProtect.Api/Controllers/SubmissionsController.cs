using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LIAnsureProtect.Application.Common.Idempotency;
using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Submissions.Commands.CreateSubmission;
using LIAnsureProtect.Application.Submissions.Commands.SubmitSubmission;
using LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;
using LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using ApplicationValidationException = LIAnsureProtect.Application.Common.Exceptions.ValidationException;

namespace LIAnsureProtect.Api.Controllers;


[ApiController]
[Route("api/v1/[controller]")]
public sealed class SubmissionsController(
    ISender sender,
    IIdempotencyService idempotencyService,
    ICurrentUser currentUser) : ControllerBase
{
    private const string IdempotencyKeyHeaderName = "Idempotency-Key";
    private static readonly JsonSerializerOptions FingerprintJsonOptions = JsonSerializerOptions.Web;

    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.ReadSubmission)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ListSubmissionsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ListSubmissionsResult>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListSubmissionsQuery(), cancellationToken);

        return Ok(result);
    }

    [HttpGet("{submissionId:guid}")]
    [Authorize(Policy = ApplicationPolicies.ReadSubmission)]
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

    [HttpPost("{submissionId:guid}/submit")]
    [Authorize(Policy = ApplicationPolicies.SubmitSubmission)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<SubmitSubmissionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SubmitSubmissionResult>> Submit(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = GetIdempotencyKey();
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var idempotencyRequest = new IdempotencyRequest(
                idempotencyKey,
                GetRequiredCurrentUserId("submit a submission"),
                ApplicationPolicies.SubmitSubmission,
                CreateRequestFingerprint(
                    HttpMethods.Post,
                    "/api/v1/submissions/{submissionId}/submit",
                    new { submissionId }));

            var executionResult = await idempotencyService.ExecuteAsync(
                idempotencyRequest,
                async operationCancellationToken =>
                {
                    try
                    {
                        var result = await sender.Send(
                            new SubmitSubmissionCommand(submissionId),
                            operationCancellationToken);

                        return result is null
                            ? IdempotencyActionResponse.Json(
                                StatusCodes.Status404NotFound,
                                CreateProblemDetails(
                                    StatusCodes.Status404NotFound,
                                    "Submission was not found."))
                            : IdempotencyActionResponse.Json(
                                StatusCodes.Status200OK,
                                result);
                    }
                    catch (InvalidOperationException exception)
                    {
                        return IdempotencyActionResponse.Json(
                            StatusCodes.Status409Conflict,
                            CreateProblemDetails(
                                StatusCodes.Status409Conflict,
                                "Submission cannot be submitted.",
                                exception.Message));
                    }
                },
                cancellationToken);

            return ToActionResult<SubmitSubmissionResult>(executionResult);
        }

        try
        {
            var result = await sender.Send(
                new SubmitSubmissionCommand(submissionId),
                cancellationToken);

            return result is null
                ? NotFound()
                : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Submission cannot be submitted.",
                Detail = exception.Message
            });
        }
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

        var idempotencyKey = GetIdempotencyKey();
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var idempotencyRequest = new IdempotencyRequest(
                idempotencyKey,
                GetRequiredCurrentUserId("create a submission"),
                ApplicationPolicies.CreateSubmission,
                CreateRequestFingerprint(
                    HttpMethods.Post,
                    "/api/v1/submissions",
                    request));

            var executionResult = await idempotencyService.ExecuteAsync(
                idempotencyRequest,
                async operationCancellationToken =>
                {
                    try
                    {
                        var result = await sender.Send(command, operationCancellationToken);
                        var location = $"/api/v1/submissions/{result.SubmissionId}";

                        return IdempotencyActionResponse.Json(
                            StatusCodes.Status201Created,
                            result,
                            location);
                    }
                    catch (ApplicationValidationException exception)
                    {
                        return IdempotencyActionResponse.Json(
                            StatusCodes.Status400BadRequest,
                            CreateValidationProblemDetails(exception));
                    }
                },
                cancellationToken);

            return ToActionResult<CreateSubmissionResult>(executionResult);
        }

        try
        {
            var result = await sender.Send(command, cancellationToken);

            return Created($"/api/v1/submissions/{result.SubmissionId}", result);
        }
        catch (ApplicationValidationException exception)
        {
            return BadRequest(CreateValidationProblemDetails(exception));
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
        object? body)
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



public sealed record CreateSubmissionRequest(
    string ApplicantName,
    string ApplicantEmail,
    string CompanyName);
