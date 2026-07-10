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

        if (submission is null)
            return null;

        var latestQuote = await dbContext.Quotes
            .AsNoTracking()
            .Where(quote => quote.SubmissionId == submission.Id && quote.OwnerUserId == ownerUserId)
            .OrderByDescending(quote => quote.CreatedAtUtc)
            .Select(quote => new
            {
                quote.Id,
                quote.Premium,
                quote.RequestedLimit,
                quote.Retention,
                quote.RiskTier,
                quote.Status,
                quote.Subjectivities,
                quote.ReferralReasons,
                quote.ExpiresAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new SubmissionDetailResult(
                submission.Id,
                submission.ApplicantName,
                submission.ApplicantEmail,
                submission.CompanyName,
                submission.Status.ToString(),
                submission.CreatedAtUtc,
                latestQuote is null
                    ? null
                    : new SubmissionQuoteSummaryResult(
                        latestQuote.Id,
                        latestQuote.Premium,
                        latestQuote.RequestedLimit,
                        latestQuote.Retention,
                        latestQuote.RiskTier.ToString(),
                        latestQuote.Status.ToString(),
                        SplitLines(latestQuote.Subjectivities),
                        SplitLines(latestQuote.ReferralReasons),
                        latestQuote.ExpiresAtUtc));
    }

    public Task<Submission?> GetOwnedForUpdateAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        return dbContext.Submissions
            .Where(submission => submission.Id == submissionId)
            .Where(submission => submission.OwnerUserId == ownerUserId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static List<string> SplitLines(string value)
    {
        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
