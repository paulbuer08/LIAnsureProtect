using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Domain.Submissions;
using MediatR;

namespace LIAnsureProtect.Application.Submissions.Commands.CreateSubmission;

public sealed class CreateSubmissionCommandHandler(
    ISubmissionRepository submissionRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<CreateSubmissionCommand, CreateSubmissionResult>
{
    public async Task<CreateSubmissionResult> Handle(
        CreateSubmissionCommand request,
        CancellationToken cancellationToken)
    {
        var submission = Submission.CreateDraft(
            request.ApplicantName,
            request.ApplicantEmail,
            request.CompanyName,
            GetRequiredCurrentUserId(),
            DateTime.UtcNow);

        await submissionRepository.AddAsync(submission, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateSubmissionResult(
            submission.Id,
            submission.Status.ToString());
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to create a submission.")
            : currentUser.UserId;
    }
}
