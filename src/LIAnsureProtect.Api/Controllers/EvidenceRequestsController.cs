using LIAnsureProtect.Application.Common.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModuleEvidence = LIAnsureProtect.Modules.Underwriting.Application.Evidence;
using ModuleEvidenceDocuments = LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;
using ModuleOwnerEvidenceRequests = LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.ListOwnerEvidenceRequests;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
[Route("api/v1/evidence-requests")]
[Authorize(Policy = ApplicationPolicies.RespondToEvidenceRequest)]
public sealed class EvidenceRequestsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ModuleEvidence.ListOwnerEvidenceRequestsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleEvidence.ListOwnerEvidenceRequestsResult>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ModuleOwnerEvidenceRequests.ListOwnerEvidenceRequestsQuery(), cancellationToken);

        return Ok(result);
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
                    request.ResponseText,
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
                    request.ResponseText,
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
    string ResponseText,
    string? AttachmentFileName,
    string? AttachmentContentType,
    long? AttachmentSizeBytes);

public sealed class RespondToEvidenceRequestFormRequest
{
    public string RespondentName { get; init; } = string.Empty;

    public string RespondentTitle { get; init; } = string.Empty;

    public string ResponseText { get; init; } = string.Empty;

    public IReadOnlyCollection<IFormFile> Attachments { get; init; } = [];
}

public sealed class UploadReplacementEvidenceDocumentsFormRequest
{
    public IReadOnlyCollection<IFormFile> Attachments { get; init; } = [];
}
