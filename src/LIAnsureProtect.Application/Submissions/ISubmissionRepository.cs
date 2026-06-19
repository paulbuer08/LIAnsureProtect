using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;
using LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;

namespace LIAnsureProtect.Application.Submissions;

public interface ISubmissionRepository
{
    Task AddAsync(Submission submission, CancellationToken cancellationToken);

    Task<IReadOnlyList<SubmissionListItemResult>> ListAsync(CancellationToken cancellationToken);

    Task<SubmissionDetailResult?> GetDetailAsync(
        Guid submissionId,
        CancellationToken cancellationToken);
}
