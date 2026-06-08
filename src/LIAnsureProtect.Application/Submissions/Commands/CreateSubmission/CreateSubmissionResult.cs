namespace LIAnsureProtect.Application.Submissions.Commands.CreateSubmission;

public sealed record CreateSubmissionResult(
    Guid SubmissionId,
    string Status);
