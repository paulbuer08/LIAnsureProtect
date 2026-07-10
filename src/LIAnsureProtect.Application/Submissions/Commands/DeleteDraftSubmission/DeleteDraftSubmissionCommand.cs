using MediatR;

namespace LIAnsureProtect.Application.Submissions.Commands.DeleteDraftSubmission;

public sealed record DeleteDraftSubmissionCommand(Guid SubmissionId) : IRequest<bool>;
