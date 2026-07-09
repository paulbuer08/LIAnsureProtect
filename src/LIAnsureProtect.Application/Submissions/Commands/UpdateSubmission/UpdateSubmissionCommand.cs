using MediatR;

namespace LIAnsureProtect.Application.Submissions.Commands.UpdateSubmission;

public sealed record UpdateSubmissionCommand(
    Guid SubmissionId,
    string ApplicantName,
    string ApplicantEmail,
    string CompanyName)
    : IRequest<UpdateSubmissionResult?>;
