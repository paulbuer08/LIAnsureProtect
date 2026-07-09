namespace LIAnsureProtect.Application.Submissions.Commands.UpdateSubmission;

public sealed record UpdateSubmissionResult(
    Guid SubmissionId,
    string ApplicantName,
    string ApplicantEmail,
    string CompanyName,
    string Status,
    DateTime CreatedAtUtc);
