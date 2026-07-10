using MediatR;

namespace LIAnsureProtect.Application.Submissions.Commands.WithdrawSubmission;

public sealed record WithdrawSubmissionCommand(Guid SubmissionId) : IRequest<WithdrawSubmissionResult?>;

public sealed record WithdrawSubmissionResult(Guid SubmissionId, string Status);
