using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;
using LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Submissions;

public sealed class EfCoreSubmissionRepository(SubmissionDbContext dbContext) : ISubmissionRepository
{
    public async Task AddAsync(Submission submission, CancellationToken cancellationToken)
    {
        await dbContext.Submissions.AddAsync(submission, cancellationToken);
    }

    public async Task<IReadOnlyList<SubmissionListItemResult>> ListAsync(
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var submissions = await dbContext.Submissions
            .AsNoTracking()
            .Where(submission => submission.OwnerUserId == ownerUserId)
            .OrderByDescending(submission => submission.CreatedAtUtc)
            .Select(submission => new
            {
                submission.Id,
                submission.ApplicantName,
                submission.ApplicantEmail,
                submission.CompanyName,
                submission.Status,
                submission.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return submissions
            .Select(submission => new SubmissionListItemResult(
                submission.Id,
                submission.ApplicantName,
                submission.ApplicantEmail,
                submission.CompanyName,
                submission.Status.ToString(),
                submission.CreatedAtUtc))
            .ToList();
    }

    public async Task<SubmissionDetailResult?> GetDetailAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var submission = await dbContext.Submissions
            .AsNoTracking()
            .Where(submission => submission.Id == submissionId)
            .Where(submission => submission.OwnerUserId == ownerUserId)
            .Select(submission => new
            {
                submission.Id,
                submission.ApplicantName,
                submission.ApplicantEmail,
                submission.CompanyName,
                submission.Status,
                submission.CreatedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        return submission is null
            ? null
            : new SubmissionDetailResult(
                submission.Id,
                submission.ApplicantName,
                submission.ApplicantEmail,
                submission.CompanyName,
                submission.Status.ToString(),
                submission.CreatedAtUtc);
    }
}
