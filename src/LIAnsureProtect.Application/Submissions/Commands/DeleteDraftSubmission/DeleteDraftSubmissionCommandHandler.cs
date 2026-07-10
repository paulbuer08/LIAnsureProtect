using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Application.Submissions.Commands.DeleteDraftSubmission;

public sealed class DeleteDraftSubmissionCommandHandler(
    ISubmissionRepository submissionRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<DeleteDraftSubmissionCommand, bool>
{
    public async Task<bool> Handle(
        DeleteDraftSubmissionCommand request,
        CancellationToken cancellationToken)
    {
        var submission = await submissionRepository.GetOwnedForUpdateAsync(
            request.SubmissionId,
            GetRequiredCurrentUserId(),
            cancellationToken);

        if (submission is null)
            return false;

        if (submission.Status != SubmissionStatus.Draft)
            throw new InvalidOperationException("Only draft submissions can be deleted. Submitted business records are retained for audit history.");

        submissionRepository.Remove(submission);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private string GetRequiredCurrentUserId() =>
        string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to delete a draft submission.")
            : currentUser.UserId;
}
