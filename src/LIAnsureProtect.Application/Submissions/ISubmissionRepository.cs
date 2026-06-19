using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;
using LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;

namespace LIAnsureProtect.Application.Submissions;

public interface ISubmissionRepository
{
    Task AddAsync(Submission submission, CancellationToken cancellationToken);

    Task<IReadOnlyList<SubmissionListItemResult>> ListAsync(
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<SubmissionDetailResult?> GetDetailAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken);
}
