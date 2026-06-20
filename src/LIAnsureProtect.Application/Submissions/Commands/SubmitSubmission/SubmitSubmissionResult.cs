namespace LIAnsureProtect.Application.Submissions.Commands.SubmitSubmission;

public sealed record SubmitSubmissionResult(
    Guid SubmissionId,
    string Status);
