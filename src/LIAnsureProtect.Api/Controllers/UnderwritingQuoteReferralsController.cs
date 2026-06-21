using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Application.Quotes.Commands.UnderwriteQuoteReferral;
using LIAnsureProtect.Application.Quotes.Queries.ListQuoteReferrals;
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
