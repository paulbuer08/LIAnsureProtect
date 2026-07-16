using System.ComponentModel.DataAnnotations;
using LIAnsureProtect.Api.Validation;
using LIAnsureProtect.Application.Common.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModuleEvidence = LIAnsureProtect.Modules.Underwriting.Application.Evidence;
using ModuleEvidenceDocuments = LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;
using ModuleEvidenceEmail = LIAnsureProtect.Modules.Underwriting.Application.Evidence.Email;
using ModuleOwnerEvidenceRequests = LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.ListOwnerEvidenceRequests;
using ModuleOwnerEvidenceRequest = LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.GetOwnerEvidenceRequest;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
[Route("api/v1/evidence-requests")]
[Authorize(Policy = ApplicationPolicies.RespondToEvidenceRequest)]
[ServiceFilter<Caching.ReferralQueueCacheInvalidationFilter>]
public sealed class EvidenceRequestsController(ISender sender) : ControllerBase
{
    [HttpPost("email-domain-check")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ModuleEvidenceEmail.EmailDomainCapabilityResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleEvidenceEmail.EmailDomainCapabilityResult>> CheckEmailDomain(
        CheckRespondentEmailDomainRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(
                new ModuleEvidenceEmail.CheckRespondentEmailDomainQuery(request.EmailAddress),
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Respondent email is invalid.",
                exception.Message));
        }
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ModuleEvidence.ListOwnerEvidenceRequestsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleEvidence.ListOwnerEvidenceRequestsResult>> List(
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] Guid? quoteId,
        [FromQuery] bool? overdue,
        [FromQuery] string? search,
        [FromQuery] string? reviewDecision,
        [FromQuery] string? documentRequirement,
        [FromQuery] string? quoteDisposition,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 12,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await sender.Send(
                new ModuleOwnerEvidenceRequests.ListOwnerEvidenceRequestsQuery(
                    status,
                    category,
                    quoteId,
                    overdue,
                    cursor,
                    pageSize,
                    search,
                    reviewDecision,
                    documentRequirement,
                    quoteDisposition ?? "Current"),
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Evidence request filters are invalid."));
        }
    }

    [HttpGet("{evidenceRequestId:guid}")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ModuleEvidence.QuoteEvidenceRequestResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleEvidence.QuoteEvidenceRequestResult>> Get(
        Guid evidenceRequestId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ModuleOwnerEvidenceRequest.GetOwnerEvidenceRequestQuery(evidenceRequestId),
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{evidenceRequestId:guid}/respond")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ModuleEvidence.QuoteEvidenceRequestResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleEvidence.QuoteEvidenceRequestResult>> Respond(
        Guid evidenceRequestId,
        RespondToEvidenceRequestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new ModuleEvidenceDocuments.RespondToQuoteEvidenceRequestCommand(
                    evidenceRequestId,
                    request.RespondentName,
                    request.RespondentTitle,
                    request.RespondentEmail,
                    request.RespondentMobileNumber,
                    request.RespondentTelephoneNumber,
                    request.ResponseText,
                    request.OtherConcerns,
                    request.AttachmentFileName,
                    request.AttachmentContentType,
                    request.AttachmentSizeBytes,
                    []),
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

    [HttpPost("{evidenceRequestId:guid}/respond")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ModuleEvidence.QuoteEvidenceRequestResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleEvidence.QuoteEvidenceRequestResult>> RespondWithDocuments(
        Guid evidenceRequestId,
        [FromForm] RespondToEvidenceRequestFormRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new ModuleEvidenceDocuments.RespondToQuoteEvidenceRequestCommand(
                    evidenceRequestId,
                    request.RespondentName,
                    request.RespondentTitle,
                    request.RespondentEmail,
                    request.RespondentMobileNumber,
                    request.RespondentTelephoneNumber,
                    request.ResponseText,
                    request.OtherConcerns,
                    null,
                    null,
                    null,
                    request.Attachments
                        .Select(file => new ModuleEvidenceDocuments.EvidenceDocumentUpload(
                            file.FileName,
                            file.ContentType,
                            file.Length,
                            file.OpenReadStream()))
                        .ToList()),
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

    [HttpPost("{evidenceRequestId:guid}/responses/{responseId:guid}/email-verification")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ModuleEvidenceEmail.RespondentEmailVerificationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleEvidenceEmail.RespondentEmailVerificationResult>> RequestEmailVerification(
        Guid evidenceRequestId,
        Guid responseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new ModuleEvidenceEmail.RequestRespondentEmailVerificationCommand(evidenceRequestId, responseId),
                cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Verification email cannot be sent.",
                exception.Message));
        }
    }

    [HttpPost("{evidenceRequestId:guid}/responses/{responseId:guid}/email-verification/verify")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ModuleEvidenceEmail.RespondentEmailVerificationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleEvidenceEmail.RespondentEmailVerificationResult>> VerifyEmail(
        Guid evidenceRequestId,
        Guid responseId,
        VerifyRespondentEmailRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new ModuleEvidenceEmail.VerifyRespondentEmailCommand(
                    evidenceRequestId,
                    responseId,
                    request.VerificationCode),
                cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Verification code is invalid.",
                exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Respondent email cannot be verified.",
                exception.Message));
        }
    }

    [HttpGet("{evidenceRequestId:guid}/documents/{documentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadDocument(
        Guid evidenceRequestId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new ModuleEvidenceDocuments.DownloadOwnerEvidenceDocumentQuery(evidenceRequestId, documentId),
                cancellationToken);

            return result is null
                ? NotFound()
                : File(result.Content, result.ContentType, result.OriginalFileName);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Evidence document cannot be downloaded.",
                exception.Message));
        }
    }

    [HttpPost("{evidenceRequestId:guid}/documents")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ModuleEvidence.QuoteEvidenceRequestResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleEvidence.QuoteEvidenceRequestResult>> UploadReplacementDocuments(
        Guid evidenceRequestId,
        [FromForm] UploadReplacementEvidenceDocumentsFormRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new ModuleEvidenceDocuments.UploadReplacementEvidenceDocumentsCommand(
                    evidenceRequestId,
                    request.Attachments
                        .Select(file => new ModuleEvidenceDocuments.EvidenceDocumentUpload(
                            file.FileName,
                            file.ContentType,
                            file.Length,
                            file.OpenReadStream()))
                        .ToList()),
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Replacement evidence documents are invalid.",
                exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Replacement evidence documents cannot be uploaded.",
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

public sealed record CheckRespondentEmailDomainRequest(
    [Required, EmailAddress, MaxLength(254)] string EmailAddress);

public sealed record VerifyRespondentEmailRequest(
    [Required, MaxLength(100)] string VerificationCode);

public sealed record RespondToEvidenceRequestRequest(
    [Required, MaxLength(120)] string RespondentName,
    [Required, MaxLength(120)] string RespondentTitle,
    [Required, EmailAddress, MaxLength(254)] string RespondentEmail,
    [MaxLength(32), PhilippineMobileNumber] string? RespondentMobileNumber,
    [MaxLength(32), PhilippineTelephoneNumber] string? RespondentTelephoneNumber,
    [MaxLength(4000)] string? ResponseText,
    [MaxLength(2000)] string? OtherConcerns,
    string? AttachmentFileName,
    string? AttachmentContentType,
    long? AttachmentSizeBytes);

public sealed class RespondToEvidenceRequestFormRequest
{
    [Required, MaxLength(120)]
    public string RespondentName { get; init; } = string.Empty;

    [Required, MaxLength(120)]
    public string RespondentTitle { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(254)]
    public string RespondentEmail { get; init; } = string.Empty;

    [MaxLength(32), PhilippineMobileNumber]
    public string? RespondentMobileNumber { get; init; }

    [MaxLength(32), PhilippineTelephoneNumber]
    public string? RespondentTelephoneNumber { get; init; }

    [MaxLength(4000)]
    public string? ResponseText { get; init; }

    [MaxLength(2000)]
    public string? OtherConcerns { get; init; }

    public IReadOnlyCollection<IFormFile> Attachments { get; init; } = [];
}

public sealed class UploadReplacementEvidenceDocumentsFormRequest
{
    public IReadOnlyCollection<IFormFile> Attachments { get; init; } = [];
}
