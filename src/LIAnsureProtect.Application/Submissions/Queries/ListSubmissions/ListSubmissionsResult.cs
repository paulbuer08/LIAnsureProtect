namespace LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;

public sealed record ListSubmissionsResult(
    IReadOnlyList<SubmissionListItemResult> Submissions,
    string? NextCursor);

public sealed record SubmissionListItemResult(
    Guid SubmissionId,
    string SubmissionReference,
    string ApplicantName,
    string ApplicantEmail,
    string CompanyName,
    string Status,
    DateTime CreatedAtUtc);

public sealed record SubmissionListFilter(
    string? Search,
    string? Status,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    string? Cursor,
    int PageSize);
