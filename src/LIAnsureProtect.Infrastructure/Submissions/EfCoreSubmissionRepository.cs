using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;
using LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;
using LIAnsureProtect.Application.Policies.Queries;
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

        var relatedPolicy = await dbContext.Policies
            .AsNoTracking()
            .Where(policy => policy.SubmissionId == submission.Id && policy.OwnerUserId == ownerUserId)
            .OrderByDescending(policy => policy.CreatedAtUtc)
            .Select(policy => new
            {
                policy.Id,
                policy.PolicyNumber,
                policy.Status,
                policy.EffectiveDateUtc,
                policy.ExpirationDateUtc
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
                        latestQuote.ExpiresAtUtc),
                relatedPolicy is null
                    ? null
                    : new SubmissionPolicySummaryResult(
                        relatedPolicy.Id,
                        relatedPolicy.PolicyNumber,
                        relatedPolicy.Status.ToString(),
                        PolicyCoverageState.Compute(
                            relatedPolicy.Status.ToString(),
                            relatedPolicy.EffectiveDateUtc,
                            relatedPolicy.ExpirationDateUtc,
                            DateTime.UtcNow),
                        relatedPolicy.EffectiveDateUtc,
                        relatedPolicy.ExpirationDateUtc));
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

    public void Remove(Submission submission)
    {
        dbContext.Submissions.Remove(submission);
    }

    public Task<bool> HasAcceptedOrBoundQuoteAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        return dbContext.Quotes.AnyAsync(
            quote => quote.SubmissionId == submissionId
                && quote.OwnerUserId == ownerUserId
                && (quote.Status == Domain.Quotes.QuoteStatus.Accepted
                    || quote.Status == Domain.Quotes.QuoteStatus.Bound),
            cancellationToken);
    }

    public Task<bool> HasOpenSubmissionForCompanyAsync(
        string ownerUserId,
        string companyName,
        CancellationToken cancellationToken)
    {
        var normalizedCompanyName = companyName.Trim();

        return dbContext.Submissions.AnyAsync(
            submission => submission.OwnerUserId == ownerUserId
                && (submission.Status == SubmissionStatus.Draft
                    || submission.Status == SubmissionStatus.Submitted)
                && submission.CompanyName == normalizedCompanyName,
            cancellationToken);
    }

    private static List<string> SplitLines(string value)
    {
        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
