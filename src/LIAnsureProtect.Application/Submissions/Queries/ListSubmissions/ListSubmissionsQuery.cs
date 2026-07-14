using MediatR;

namespace LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;

public sealed record ListSubmissionsQuery(
    string? Search = null,
    string? Status = null,
    DateTime? CreatedFromUtc = null,
    DateTime? CreatedToUtc = null,
    string? Cursor = null,
    int PageSize = 20) : IRequest<ListSubmissionsResult>;
