using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;

public sealed class ListSubmissionsQueryHandler(
    ISubmissionRepository submissionRepository,
    ICurrentUser currentUser)
    : IRequestHandler<ListSubmissionsQuery, ListSubmissionsResult>
{
    public async Task<ListSubmissionsResult> Handle(
        ListSubmissionsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.PageSize is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(request), "Page size must be between 1 and 50.");

        if (!string.IsNullOrWhiteSpace(request.Search) && request.Search.Trim().Length > 200)
            throw new ArgumentException("Search text must not exceed 200 characters.", nameof(request));

        return await submissionRepository.ListAsync(
            GetRequiredCurrentUserId(),
            new SubmissionListFilter(
                request.Search?.Trim(),
                request.Status?.Trim(),
                request.CreatedFromUtc,
                request.CreatedToUtc,
                request.Cursor,
                request.PageSize),
            cancellationToken);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to list submissions.")
            : currentUser.UserId;
    }
}
