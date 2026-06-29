using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Application.Submissions.Commands.SubmitSubmission;

public sealed class SubmitSubmissionCommandHandler(
    ISubmissionRepository submissionRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<SubmitSubmissionCommand, SubmitSubmissionResult?>
{
    public async Task<SubmitSubmissionResult?> Handle(
        SubmitSubmissionCommand request,
        CancellationToken cancellationToken)
    {
        var submission = await submissionRepository.GetOwnedForUpdateAsync(
            request.SubmissionId,
            GetRequiredCurrentUserId(),
            cancellationToken);

        if (submission is null)
            return null;

        submission.Submit();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SubmitSubmissionResult(
            submission.Id,
            submission.Status.ToString());
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to submit a submission.")
            : currentUser.UserId;
    }
}
