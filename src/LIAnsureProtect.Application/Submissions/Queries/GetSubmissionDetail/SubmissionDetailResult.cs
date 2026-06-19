namespace LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;

public sealed record SubmissionDetailResult(
    Guid SubmissionId,
    string ApplicantName,
    string ApplicantEmail,
    string CompanyName,
    string Status,
    DateTime CreatedAtUtc);
