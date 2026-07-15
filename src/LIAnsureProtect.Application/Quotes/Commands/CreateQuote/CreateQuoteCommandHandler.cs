using LIAnsureProtect.Application.Common.Exceptions;
using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Quotes.Assurance;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.CreateQuote;

public sealed class CreateQuoteCommandHandler(
    ISubmissionRepository submissionRepository,
    IQuoteRepository quoteRepository,
    IReassessmentRequestRepository reassessmentRequests,
    IQuoteCreationService quoteCreationService,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<CreateQuoteCommand, CreateQuoteResult?>
{
    public async Task<CreateQuoteResult?> Handle(CreateQuoteCommand request, CancellationToken cancellationToken)
    {
        var ownerUserId = GetRequiredCurrentUserId();
        var submission = await submissionRepository.GetOwnedForUpdateAsync(
            request.SubmissionId,
            ownerUserId,
            cancellationToken);
        if (submission is null)
            return null;
        if (submission.Status != SubmissionStatus.Submitted)
            throw new InvalidOperationException("Only submitted submissions can be quoted.");

        var existingQuote = await quoteRepository.GetLatestOwnedForSubmissionAsync(
            submission.Id,
            ownerUserId,
            cancellationToken);
        if (existingQuote is not null && !request.IsReassessment)
        {
            return QuoteCreationService.ToResult(
                existingQuote,
                QuoteCreationService.CreateExistingQuoteProviderIndication(existingQuote));
        }
        if (existingQuote is null && request.IsReassessment)
            throw new BusinessConflictException("quote.reassessment.base_missing", "Create the first quote before requesting a reassessment.");

        var assertionDecisions = quoteCreationService.EvaluateAssertions(request, existingQuote);
        var nowUtc = DateTime.UtcNow;

        if (request.IsReassessment && existingQuote is not null)
        {
            ValidateBaseVersion(request, existingQuote);
            var pending = await reassessmentRequests.GetPendingOwnedAsync(
                submission.Id,
                ownerUserId,
                cancellationToken);
            if (pending is not null)
            {
                throw new BusinessConflictException(
                    "quote.reassessment.pending_exists",
                    "A reassessment request is already waiting for underwriting review.");
            }

            var cooldownRemaining = ReassessmentGovernancePolicy.GetCooldownRemaining(
                existingQuote.Version,
                existingQuote.CreatedAtUtc,
                nowUtc);
            if (cooldownRemaining.HasValue)
            {
                var minutesRemaining = Math.Max(1, (int)Math.Ceiling(cooldownRemaining.Value.TotalMinutes));
                throw new BusinessConflictException(
                    "quote.reassessment.cooldown",
                    $"A successful reassessment was created recently. Try again in about {minutesRemaining} minute{(minutesRemaining == 1 ? string.Empty : "s")}.");
            }

            var rollingCount = await reassessmentRequests.CountSuccessfulOwnedAsync(
                submission.Id,
                ownerUserId,
                nowUtc.AddHours(-ReassessmentGovernancePolicy.RollingWindowHours),
                cancellationToken);
            var lifetimeCount = await reassessmentRequests.CountSuccessfulOwnedAsync(
                submission.Id,
                ownerUserId,
                null,
                cancellationToken);

            if (ReassessmentGovernancePolicy.RequiresManualReview(
                    rollingCount,
                    lifetimeCount))
            {
                var payload = ReassessmentRequestPayloadSerializer.Serialize(request);
                var reassessmentRequest = ReassessmentRequest.Create(
                    submission.Id,
                    existingQuote.Id,
                    existingQuote.Version,
                    ownerUserId,
                    payload,
                    ReassessmentRequestPayloadSerializer.Fingerprint(payload),
                    ownerUserId,
                    nowUtc,
                    submission.Reference,
                    submission.CompanyName);
                await reassessmentRequests.AddAsync(reassessmentRequest, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                throw new ReassessmentReviewQueuedException(new ReassessmentReviewQueuedResult(
                    reassessmentRequest.Id,
                    reassessmentRequest.SubmissionId,
                    reassessmentRequest.BaseQuoteId,
                    reassessmentRequest.BaseQuoteVersion,
                    reassessmentRequest.Status.ToString(),
                    reassessmentRequest.RequestedAtUtc,
                    "This reassessment is queued for underwriting review. The current quote remains active until approval."));
            }
        }

        var result = await quoteCreationService.CreateAsync(
            submission,
            existingQuote,
            request,
            assertionDecisions,
            nowUtc,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static void ValidateBaseVersion(CreateQuoteCommand request, Quote existingQuote)
    {
        if (!request.BaseQuoteVersion.HasValue)
        {
            throw new BusinessConflictException(
                "quote.reassessment.base_required",
                "Refresh the submission and start reassessment from the current quote version.");
        }

        if (request.BaseQuoteVersion.Value != existingQuote.Version)
        {
            throw new BusinessConflictException(
                "quote.reassessment.stale_base",
                $"Quote version {existingQuote.Version} is now current. Refresh before requesting another reassessment.");
        }
    }

    private string GetRequiredCurrentUserId()
        => string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to create a quote.")
            : currentUser.UserId;
}
