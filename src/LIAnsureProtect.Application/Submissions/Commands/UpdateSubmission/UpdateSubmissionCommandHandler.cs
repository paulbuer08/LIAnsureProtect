using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Application.Submissions.Commands.UpdateSubmission;

public sealed class UpdateSubmissionCommandHandler(
    ISubmissionRepository submissionRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<UpdateSubmissionCommand, UpdateSubmissionResult?>
{
    public async Task<UpdateSubmissionResult?> Handle(
        UpdateSubmissionCommand request,
        CancellationToken cancellationToken)
    {
        var submission = await submissionRepository.GetOwnedForUpdateAsync(
            request.SubmissionId,
            GetRequiredCurrentUserId(),
            cancellationToken);

        if (submission is null)
            return null;

        submission.UpdateDraftDetails(
            request.ApplicantName,
            request.ApplicantEmail,
            request.CompanyName);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateSubmissionResult(
            submission.Id,
            submission.ApplicantName,
            submission.ApplicantEmail,
            submission.CompanyName,
            submission.Status.ToString(),
            submission.CreatedAtUtc);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to update a submission.")
            : currentUser.UserId;
    }
}
