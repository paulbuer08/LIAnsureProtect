using MediatR;

namespace LIAnsureProtect.Application.Submissions.Commands.SubmitSubmission;

public sealed record SubmitSubmissionCommand(Guid SubmissionId) : IRequest<SubmitSubmissionResult?>;
