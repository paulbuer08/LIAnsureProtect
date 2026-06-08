using LIAnsureProtect.Domain.Submissions;

namespace LIAnsureProtect.Application.Submissions;

public interface ISubmissionRepository
{
    Task AddAsync(Submission submission, CancellationToken cancellationToken);
}
