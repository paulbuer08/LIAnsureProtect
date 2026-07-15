using System.ComponentModel.DataAnnotations;
using LIAnsureProtect.Application.Common.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModuleEvidence = LIAnsureProtect.Modules.Underwriting.Application.Evidence;
using ModuleEvidenceDocuments = LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;
using ModuleOwnerEvidenceRequests = LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.ListOwnerEvidenceRequests;
using ModuleOwnerEvidenceRequest = LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.GetOwnerEvidenceRequest;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
[Route("api/v1/evidence-requests")]
[Authorize(Policy = ApplicationPolicies.RespondToEvidenceRequest)]
[ServiceFilter<Caching.ReferralQueueCacheInvalidationFilter>]
public sealed class EvidenceRequestsController(ISender sender) : ControllerBase
{
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
                    documentRequirement),
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

public sealed record RespondToEvidenceRequestRequest(
    string RespondentName,
    string RespondentTitle,
    string RespondentEmail,
    string? RespondentMobileNumber,
    string? RespondentTelephoneNumber,
    string? ResponseText,
    string? OtherConcerns,
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

    [MaxLength(32)]
    public string? RespondentMobileNumber { get; init; }

    [MaxLength(32)]
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
