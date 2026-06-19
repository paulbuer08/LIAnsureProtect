namespace LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;

public sealed record ListSubmissionsResult(
    IReadOnlyList<SubmissionListItemResult> Submissions);

public sealed record SubmissionListItemResult(
    Guid SubmissionId,
    string ApplicantName,
    string ApplicantEmail,
    string CompanyName,
    string Status,
    DateTime CreatedAtUtc);
