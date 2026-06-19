using MediatR;

namespace LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;

public sealed class ListSubmissionsQueryHandler(ISubmissionRepository submissionRepository)
    : IRequestHandler<ListSubmissionsQuery, ListSubmissionsResult>
{
    public async Task<ListSubmissionsResult> Handle(
        ListSubmissionsQuery request,
        CancellationToken cancellationToken)
    {
        var submissions = await submissionRepository.ListAsync(cancellationToken);

        return new ListSubmissionsResult(submissions);
    }
}
