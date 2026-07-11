using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Platform.Abstractions.Security;
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
        var ownerUserId = GetRequiredCurrentUserId();

        if (!request.CreateAnotherDraft)
        {
            var matchingDraftId = await submissionRepository.FindMatchingDraftIdAsync(
                ownerUserId,
                request.ApplicantName,
                request.ApplicantEmail,
                request.CompanyName,
                cancellationToken);

            if (matchingDraftId.HasValue)
            {
                return new CreateSubmissionResult(
                    matchingDraftId.Value,
                    nameof(SubmissionStatus.Draft),
                    PossibleDuplicate: true,
                    ExistingDraft: true);
            }
        }

        var possibleDuplicate = await submissionRepository.HasOpenSubmissionForCompanyAsync(
            ownerUserId,
            request.CompanyName,
            cancellationToken);
        var submission = Submission.CreateDraft(
            request.ApplicantName,
            request.ApplicantEmail,
            request.CompanyName,
            ownerUserId,
            DateTime.UtcNow);

        await submissionRepository.AddAsync(submission, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateSubmissionResult(
            submission.Id,
            submission.Status.ToString(),
            possibleDuplicate,
            ExistingDraft: false);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to create a submission.")
            : currentUser.UserId;
    }
}
