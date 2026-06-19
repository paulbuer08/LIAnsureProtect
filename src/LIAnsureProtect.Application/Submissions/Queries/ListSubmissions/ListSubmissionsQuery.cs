using MediatR;

namespace LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;

public sealed record ListSubmissionsQuery : IRequest<ListSubmissionsResult>;
