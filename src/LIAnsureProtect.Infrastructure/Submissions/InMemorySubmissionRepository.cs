using System.Collections.Concurrent;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Domain.Submissions;

namespace LIAnsureProtect.Infrastructure.Submissions;

public sealed class InMemorySubmissionRepository : ISubmissionRepository
{
    private readonly ConcurrentDictionary<Guid, Submission> submissions = [];

    public Task AddAsync(Submission submission, CancellationToken cancellationToken)
    {
        submissions[submission.Id] = submission;

        return Task.CompletedTask;
    }
}
