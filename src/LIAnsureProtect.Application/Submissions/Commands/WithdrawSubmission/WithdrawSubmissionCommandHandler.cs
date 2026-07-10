using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Application.Submissions.Commands.WithdrawSubmission;

public sealed class WithdrawSubmissionCommandHandler(
    ISubmissionRepository submissionRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<WithdrawSubmissionCommand, WithdrawSubmissionResult?>
{
    public async Task<WithdrawSubmissionResult?> Handle(
        WithdrawSubmissionCommand request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = GetRequiredCurrentUserId();
        var submission = await submissionRepository.GetOwnedForUpdateAsync(
            request.SubmissionId,
            ownerUserId,
            cancellationToken);

        if (submission is null)
            return null;

        if (await submissionRepository.HasAcceptedOrBoundQuoteAsync(
            submission.Id,
            ownerUserId,
            cancellationToken))
        {
            throw new InvalidOperationException("A submission cannot be withdrawn after its quote has been accepted or bound.");
        }

        submission.Withdraw(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new WithdrawSubmissionResult(submission.Id, submission.Status.ToString());
    }

    private string GetRequiredCurrentUserId() =>
        string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to withdraw a submission.")
            : currentUser.UserId;
}
