using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;
using LIAnsureProtect.Modules.Quoting.Application.ReferralDecisions;
using LIAnsureProtect.Modules.Underwriting.Application.Commands.GenerateAiUnderwritingReview;
using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Commands.ManageEvidenceRequests;
using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;
using LIAnsureProtect.Modules.Underwriting.Application.Referrals.Commands.ManageReferralOperations;
using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApplicationValidationException = LIAnsureProtect.Application.Common.Exceptions.ValidationException;
using GetQuoteReferralTimelineQuery = LIAnsureProtect.Application.Quotes.Commands.ManageQuoteReferralOperations.GetQuoteReferralTimelineQuery;
using QuoteReferralTimelineResult = LIAnsureProtect.Application.Quotes.Commands.ManageQuoteReferralOperations.QuoteReferralTimelineResult;
using EvidenceRequestCategory = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.EvidenceRequestCategory;
using EvidenceDocumentRequirement = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.EvidenceDocumentRequirement;
using EvidenceReviewDecisionStatus = LIAnsureProtect.Modules.Underwriting.Domain.Evidence.EvidenceReviewDecisionStatus;
using ModuleQuoteEvidenceRequestResult = LIAnsureProtect.Modules.Underwriting.Application.Evidence.QuoteEvidenceRequestResult;
using ReferralOperationStatus = LIAnsureProtect.Modules.Underwriting.Domain.Referrals.ReferralOperationStatus;
using ReferralPriority = LIAnsureProtect.Modules.Underwriting.Domain.Referrals.ReferralPriority;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
[Route("api/v1/underwriting/quote-referrals")]
[Authorize(Policy = ApplicationPolicies.UnderwriteQuote)]
[ServiceFilter<Caching.ReferralQueueCacheInvalidationFilter>]
public sealed class UnderwritingQuoteReferralsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ListQuoteReferralsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ListQuoteReferralsResult>> List(
        [FromQuery] string? search,
        [FromQuery] string? riskTier,
        [FromQuery] string? priority,
        [FromQuery] string? assignment,
        [FromQuery] string? evidenceState,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new SearchQuoteReferralsQuery(search, riskTier, priority, assignment, evidenceState),
                cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Underwriting filters are invalid.",
                exception.Message));
        }
    }

    [HttpPost("{quoteId:guid}/evidence-requests")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ModuleQuoteEvidenceRequestResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ModuleQuoteEvidenceRequestResult>> CreateEvidenceRequest(
        Guid quoteId,
        CreateQuoteEvidenceRequestRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<EvidenceRequestCategory>(request.Category, ignoreCase: true, out var category))
            return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Evidence request category is invalid."));

        if (!Enum.TryParse<EvidenceDocumentRequirement>(
                request.DocumentRequirement,
                ignoreCase: true,
                out var documentRequirement)
            || !Enum.IsDefined(documentRequirement))
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Evidence document requirement is invalid."));
        }

        try
        {
            var result = await sender.Send(
                new CreateQuoteEvidenceRequestCommand(
                    quoteId,
                    category,
                    request.Title,
                    request.Description,
                    request.DueAtUtc,
                    documentRequirement),
                cancellationToken);

            return result is null
                ? NotFound()
                : Created($"{Request.Path}/{result.EvidenceRequestId}", result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Evidence request is invalid.", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(StatusCodes.Status409Conflict, "Evidence request cannot be created.", exception.Message));
        }
    }

    [HttpPost("{quoteId:guid}/evidence-requests/{evidenceRequestId:guid}/accept")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ModuleQuoteEvidenceRequestResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleQuoteEvidenceRequestResult>> AcceptEvidenceRequest(
        Guid quoteId,
        Guid evidenceRequestId,
        ReviewQuoteEvidenceRequestRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteEvidenceReviewAsync(
            new AcceptQuoteEvidenceRequestCommand(quoteId, evidenceRequestId, request.ReviewNotes),
            cancellationToken);
    }

    [HttpPost("{quoteId:guid}/evidence-requests/{evidenceRequestId:guid}/review-decision")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ModuleQuoteEvidenceRequestResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleQuoteEvidenceRequestResult>> RecordEvidenceReviewDecision(
        Guid quoteId,
        Guid evidenceRequestId,
        RecordQuoteEvidenceReviewDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<EvidenceReviewDecisionStatus>(request.Decision, ignoreCase: true, out var decision)
            || decision == EvidenceReviewDecisionStatus.NotReviewed)
        {
            return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Evidence review decision is invalid."));
        }

        return await ExecuteEvidenceReviewAsync(
            new RecordQuoteEvidenceReviewDecisionCommand(
                quoteId,
                evidenceRequestId,
                decision.ToString(),
                request.Reason,
                request.RemediationGuidance),
            cancellationToken);
    }

    [HttpPost("{quoteId:guid}/evidence-requests/{evidenceRequestId:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ModuleQuoteEvidenceRequestResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleQuoteEvidenceRequestResult>> CancelEvidenceRequest(
        Guid quoteId,
        Guid evidenceRequestId,
        ReviewQuoteEvidenceRequestRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteEvidenceRequestAsync(
            new CancelQuoteEvidenceRequestCommand(quoteId, evidenceRequestId, request.ReviewNotes),
            cancellationToken);
    }

    [HttpPost("{quoteId:guid}/evidence-requests/{evidenceRequestId:guid}/follow-up")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ModuleQuoteEvidenceRequestResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModuleQuoteEvidenceRequestResult>> FollowUpEvidenceRequest(
        Guid quoteId,
        Guid evidenceRequestId,
        CancellationToken cancellationToken)
    {
        return await ExecuteEvidenceRequestAsync(
            new FollowUpQuoteEvidenceRequestCommand(quoteId, evidenceRequestId),
            cancellationToken);
    }

    [HttpGet("{quoteId:guid}/evidence-requests/{evidenceRequestId:guid}/documents/{documentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadEvidenceDocument(
        Guid quoteId,
        Guid evidenceRequestId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new DownloadUnderwritingEvidenceDocumentQuery(quoteId, evidenceRequestId, documentId),
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

    [HttpPost("{quoteId:guid}/ai-review")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<GenerateAiUnderwritingReviewResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GenerateAiUnderwritingReviewResult>> GenerateAiReview(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new GenerateAiUnderwritingReviewCommand(quoteId),
                cancellationToken);

            return result is null
                ? NotFound()
                : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "AI underwriting review cannot be generated.",
                exception.Message));
        }
    }

    [HttpPost("{quoteId:guid}/operations/assign-to-me")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<QuoteReferralOperationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteReferralOperationResult>> AssignToMe(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        return await ExecuteOperationAsync(
            new AssignQuoteReferralToMeCommand(quoteId),
            cancellationToken);
    }

    [HttpPost("{quoteId:guid}/operations/release-assignment")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<QuoteReferralOperationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteReferralOperationResult>> ReleaseAssignment(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        return await ExecuteOperationAsync(
            new ReleaseQuoteReferralAssignmentCommand(quoteId),
            cancellationToken);
    }

    [HttpPost("{quoteId:guid}/operations/triage")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<QuoteReferralOperationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteReferralOperationResult>> Triage(
        Guid quoteId,
        QuoteReferralTriageRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ReferralPriority>(request.Priority, ignoreCase: true, out var priority))
            return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Referral priority is invalid."));

        if (!Enum.TryParse<ReferralOperationStatus>(request.Status, ignoreCase: true, out var status))
            return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Referral operation status is invalid."));

        return await ExecuteOperationAsync(
            new TriageQuoteReferralOperationCommand(quoteId, priority, status, request.DueAtUtc),
            cancellationToken);
    }

    [HttpPost("{quoteId:guid}/operations/notes")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<QuoteReferralNoteResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<QuoteReferralNoteResult>> AddNote(
        Guid quoteId,
        QuoteReferralNoteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new AddQuoteReferralNoteCommand(quoteId, request.Note),
                cancellationToken);

            return result is null
                ? NotFound()
                : Created($"{Request.Path}/{result.NoteId}", result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Quote referral note request is invalid.", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(StatusCodes.Status409Conflict, "Quote referral operations cannot be updated.", exception.Message));
        }
    }

    [HttpGet("{quoteId:guid}/operations/timeline")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<QuoteReferralTimelineResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteReferralTimelineResult>> GetTimeline(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetQuoteReferralTimelineQuery(quoteId),
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{quoteId:guid}/operations/tasks")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<QuoteReferralTaskResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<QuoteReferralTaskResult>> AddTask(
        Guid quoteId,
        QuoteReferralTaskRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new AddQuoteReferralTaskCommand(quoteId, request.Title, request.DueAtUtc),
                cancellationToken);

            return result is null
                ? NotFound()
                : Created($"{Request.Path}/{result.TaskId}", result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Quote referral task request is invalid.", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(StatusCodes.Status409Conflict, "Quote referral operations cannot be updated.", exception.Message));
        }
    }

    [HttpPost("{quoteId:guid}/operations/tasks/{taskId:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<QuoteReferralTaskResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteReferralTaskResult>> CompleteTask(
        Guid quoteId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new CompleteQuoteReferralTaskCommand(quoteId, taskId),
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(StatusCodes.Status409Conflict, "Quote referral operations cannot be updated.", exception.Message));
        }
    }

    [HttpPost("{quoteId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<UnderwriteQuoteReferralResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnderwriteQuoteReferralResult>> Approve(
        Guid quoteId,
        QuoteReferralReviewRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteReviewAsync(
            new ApproveQuoteReferralCommand(quoteId, request.Reason, request.Notes),
            cancellationToken);
    }

    [HttpPost("{quoteId:guid}/decline")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<UnderwriteQuoteReferralResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnderwriteQuoteReferralResult>> Decline(
        Guid quoteId,
        QuoteReferralReviewRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteReviewAsync(
            new DeclineQuoteReferralCommand(quoteId, request.Reason, request.Notes),
            cancellationToken);
    }

    [HttpPost("{quoteId:guid}/adjust")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<UnderwriteQuoteReferralResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnderwriteQuoteReferralResult>> Adjust(
        Guid quoteId,
        AdjustQuoteReferralRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteReviewAsync(
            new AdjustQuoteReferralCommand(
                quoteId,
                request.AdjustedPremium,
                request.AdjustedRetention,
                request.Reason,
                request.Notes,
                request.UpdatedSubjectivities),
            cancellationToken);
    }

    private async Task<ActionResult<UnderwriteQuoteReferralResult>> ExecuteReviewAsync(
        IRequest<UnderwriteQuoteReferralResult?> command,
        CancellationToken cancellationToken)
    {
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
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Quote referral review request is invalid.",
                exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Quote referral cannot be reviewed.",
                exception.Message));
        }
    }

    private async Task<ActionResult<QuoteReferralOperationResult>> ExecuteOperationAsync(
        IRequest<QuoteReferralOperationResult?> command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(command, cancellationToken);

            return result is null
                ? NotFound()
                : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Quote referral operations request is invalid.",
                exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Quote referral operations cannot be updated.",
                exception.Message));
        }
    }

    private async Task<ActionResult<ModuleQuoteEvidenceRequestResult>> ExecuteEvidenceReviewAsync(
        IRequest<ModuleQuoteEvidenceRequestResult?> command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(command, cancellationToken);

            return result is null
                ? NotFound()
                : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Evidence request review is invalid.",
                exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Evidence request cannot be reviewed.",
                exception.Message));
        }
    }

    private async Task<ActionResult<ModuleQuoteEvidenceRequestResult>> ExecuteEvidenceRequestAsync(
        IRequest<ModuleQuoteEvidenceRequestResult?> command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(command, cancellationToken);

            return result is null
                ? NotFound()
                : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Evidence request is invalid.",
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

public sealed record QuoteReferralReviewRequest(
    string Reason,
    string? Notes);

public sealed record AdjustQuoteReferralRequest(
    decimal AdjustedPremium,
    decimal AdjustedRetention,
    string Reason,
    string? Notes,
    string? UpdatedSubjectivities);

public sealed record QuoteReferralTriageRequest(
    string Priority,
    string Status,
    DateTime DueAtUtc);

public sealed record QuoteReferralNoteRequest(string Note);

public sealed record QuoteReferralTaskRequest(
    string Title,
    DateTime DueAtUtc);

public sealed record CreateQuoteEvidenceRequestRequest(
    string Category,
    string Title,
    string Description,
    DateTime DueAtUtc,
    string DocumentRequirement = "Optional");

public sealed record ReviewQuoteEvidenceRequestRequest(string? ReviewNotes);

public sealed record RecordQuoteEvidenceReviewDecisionRequest(
    string Decision,
    string Reason,
    string? RemediationGuidance);
