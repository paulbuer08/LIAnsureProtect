using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Quotes.Commands.GenerateAiUnderwritingReview;
using LIAnsureProtect.Application.Quotes.Commands.ManageQuoteReferralOperations;
using LIAnsureProtect.Application.Quotes.Commands.UnderwriteQuoteReferral;
using LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;
using LIAnsureProtect.Domain.Quotes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApplicationValidationException = LIAnsureProtect.Application.Common.Exceptions.ValidationException;

namespace LIAnsureProtect.Api.Controllers;

[ApiController]
[Route("api/v1/underwriting/quote-referrals")]
[Authorize(Policy = ApplicationPolicies.UnderwriteQuote)]
public sealed class UnderwritingQuoteReferralsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ListQuoteReferralsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ListQuoteReferralsResult>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListQuoteReferralsQuery(), cancellationToken);

        return Ok(result);
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
