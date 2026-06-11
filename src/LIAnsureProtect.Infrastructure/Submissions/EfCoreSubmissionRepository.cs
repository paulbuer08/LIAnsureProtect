using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Infrastructure.Persistence;

namespace LIAnsureProtect.Infrastructure.Submissions;

public sealed class EfCoreSubmissionRepository(SubmissionDbContext dbContext) : ISubmissionRepository
{
    public async Task AddAsync(Submission submission, CancellationToken cancellationToken)
    {
        await dbContext.Submissions.AddAsync(submission, cancellationToken);
    }
}
