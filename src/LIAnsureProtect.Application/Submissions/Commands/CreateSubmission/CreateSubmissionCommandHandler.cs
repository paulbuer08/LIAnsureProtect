using LIAnsureProtect.Domain.Submissions;
using MediatR;

namespace LIAnsureProtect.Application.Submissions.Commands.CreateSubmission;

public sealed class CreateSubmissionCommandHandler(
    ISubmissionRepository submissionRepository)
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
            DateTime.UtcNow);

        await submissionRepository.AddAsync(submission, cancellationToken);

        return new CreateSubmissionResult(
            submission.Id,
            submission.Status.ToString());
    }
}
