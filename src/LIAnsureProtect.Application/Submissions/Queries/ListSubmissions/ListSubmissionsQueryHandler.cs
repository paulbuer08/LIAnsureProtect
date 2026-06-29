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
        var submissions = await submissionRepository.ListAsync(
            GetRequiredCurrentUserId(),
            cancellationToken);

        return new ListSubmissionsResult(submissions);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to list submissions.")
            : currentUser.UserId;
    }
}
