using LIAnsureProtect.Application.Common.Exceptions;
using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Quotes.Commands.CreateQuote;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Reassessments;

public sealed record ApproveReassessmentRequestCommand(Guid ReassessmentRequestId, string Reason)
    : IRequest<ReassessmentRequestResult?>;

public sealed record DeclineReassessmentRequestCommand(Guid ReassessmentRequestId, string Reason)
    : IRequest<ReassessmentRequestResult?>;

public sealed class ApproveReassessmentRequestCommandHandler(
    IReassessmentRequestRepository reassessmentRequests,
    ISubmissionRepository submissions,
    IQuoteRepository quotes,
    IQuoteCreationService quoteCreationService,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<ApproveReassessmentRequestCommand, ReassessmentRequestResult?>
{
    public async Task<ReassessmentRequestResult?> Handle(
        ApproveReassessmentRequestCommand command,
        CancellationToken cancellationToken)
    {
        var reviewer = RequiredUser(currentUser);
        var request = await reassessmentRequests.GetForReviewAsync(command.ReassessmentRequestId, cancellationToken);
        if (request is null)
            return null;
        if (request.Status != ReassessmentRequestStatus.Pending)
            throw new BusinessConflictException("quote.reassessment.not_pending", "This reassessment request is no longer pending.");

        var nowUtc = DateTime.UtcNow;
        var latestQuote = await quotes.GetLatestOwnedForSubmissionAsync(
            request.SubmissionId,
            request.OwnerUserId,
            cancellationToken);
        if (latestQuote is null || latestQuote.Id != request.BaseQuoteId || latestQuote.Version != request.BaseQuoteVersion)
        {
            request.MarkStale(reviewer, "A newer quote version already exists.", nowUtc);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new BusinessConflictException(
                "quote.reassessment.stale_request",
                "This request is based on an older quote version and has been closed as stale.");
        }

        var submission = await submissions.GetOwnedForUpdateAsync(
            request.SubmissionId,
            request.OwnerUserId,
            cancellationToken);
        if (submission is null)
            return null;
        if (submission.Status != SubmissionStatus.Submitted)
            throw new BusinessConflictException("quote.reassessment.submission_ineligible", "This submission can no longer be reassessed.");

        var payload = ReassessmentRequestPayloadSerializer.Deserialize(request.RequestPayloadJson) with
        {
            IsReassessment = true,
            BaseQuoteVersion = request.BaseQuoteVersion
        };
        var assertions = quoteCreationService.EvaluateAssertions(payload, latestQuote);
        var quoteResult = await quoteCreationService.CreateAsync(
            submission,
            latestQuote,
            payload,
            assertions,
            nowUtc,
            cancellationToken);
        request.Approve(quoteResult.QuoteId, reviewer, command.Reason, nowUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ReassessmentRequestResultFactory.FromRequest(request);
    }

    private static string RequiredUser(ICurrentUser user)
        => string.IsNullOrWhiteSpace(user.UserId)
            ? throw new InvalidOperationException("An authenticated underwriter user id is required.")
            : user.UserId;
}

public sealed class DeclineReassessmentRequestCommandHandler(
    IReassessmentRequestRepository reassessmentRequests,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<DeclineReassessmentRequestCommand, ReassessmentRequestResult?>
{
    public async Task<ReassessmentRequestResult?> Handle(
        DeclineReassessmentRequestCommand command,
        CancellationToken cancellationToken)
    {
        var request = await reassessmentRequests.GetForReviewAsync(command.ReassessmentRequestId, cancellationToken);
        if (request is null)
            return null;

        var reviewer = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated underwriter user id is required.")
            : currentUser.UserId;
        request.Decline(reviewer, command.Reason, DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ReassessmentRequestResultFactory.FromRequest(request);
    }
}
