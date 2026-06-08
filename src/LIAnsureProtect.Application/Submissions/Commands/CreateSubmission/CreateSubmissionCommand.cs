using MediatR;

namespace LIAnsureProtect.Application.Submissions.Commands.CreateSubmission;

public sealed record CreateSubmissionCommand(
    string ApplicantName,
    string ApplicantEmail,
    string CompanyName)
    : IRequest<CreateSubmissionResult>;
