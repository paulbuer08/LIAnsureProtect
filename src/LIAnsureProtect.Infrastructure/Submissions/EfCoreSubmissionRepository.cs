using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;
using LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;
using LIAnsureProtect.Application.Policies.Queries;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace LIAnsureProtect.Infrastructure.Submissions;

public sealed class EfCoreSubmissionRepository(SubmissionDbContext dbContext) : ISubmissionRepository
{
    public async Task AddAsync(Submission submission, CancellationToken cancellationToken)
    {
        await dbContext.Submissions.AddAsync(submission, cancellationToken);
    }

    public async Task<ListSubmissionsResult> ListAsync(
        string ownerUserId,
        SubmissionListFilter filter,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Submissions
            .AsNoTracking()
            .Where(submission => submission.OwnerUserId == ownerUserId);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var searchPattern = $"%{filter.Search.ToUpperInvariant()}%";
            var exactId = Guid.TryParse(filter.Search, out var parsedId) ? parsedId : (Guid?)null;
#pragma warning disable CA1304, CA1311 // Translated by EF into provider-side UPPER; no process culture is used.
            query = query.Where(submission =>
                EF.Functions.Like(submission.Reference.ToUpper(), searchPattern)
                || EF.Functions.Like(submission.ApplicantName.ToUpper(), searchPattern)
                || EF.Functions.Like(submission.ApplicantEmail.ToUpper(), searchPattern)
                || EF.Functions.Like(submission.CompanyName.ToUpper(), searchPattern)
                || (exactId.HasValue && submission.Id == exactId.Value));
#pragma warning restore CA1304, CA1311
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            if (!Enum.TryParse<SubmissionStatus>(filter.Status, true, out var status))
                throw new ArgumentException("Submission status is not recognized.", nameof(filter));
            query = query.Where(submission => submission.Status == status);
        }

        if (filter.CreatedFromUtc.HasValue)
            query = query.Where(submission => submission.CreatedAtUtc >= filter.CreatedFromUtc.Value);

        if (filter.CreatedToUtc.HasValue)
            query = query.Where(submission => submission.CreatedAtUtc <= filter.CreatedToUtc.Value);

        if (!string.IsNullOrWhiteSpace(filter.Cursor))
        {
            var cursor = DecodeCursor(filter.Cursor);
            query = query.Where(submission =>
                submission.CreatedAtUtc < cursor.CreatedAtUtc
                || (submission.CreatedAtUtc == cursor.CreatedAtUtc && submission.Id.CompareTo(cursor.SubmissionId) < 0));
        }

        var submissions = await query
            .OrderByDescending(submission => submission.CreatedAtUtc)
            .ThenByDescending(submission => submission.Id)
            .Take(filter.PageSize + 1)
            .Select(submission => new
            {
                submission.Id,
                submission.Reference,
                submission.ApplicantName,
                submission.ApplicantEmail,
                submission.CompanyName,
                submission.Status,
                submission.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var hasMore = submissions.Count > filter.PageSize;
        var page = submissions.Take(filter.PageSize).ToList();
        var items = page
            .Select(submission => new SubmissionListItemResult(
                submission.Id,
                submission.Reference,
                submission.ApplicantName,
                submission.ApplicantEmail,
                submission.CompanyName,
                submission.Status.ToString(),
                submission.CreatedAtUtc))
            .ToList();

        var nextCursor = hasMore && page.Count > 0
            ? EncodeCursor(page[^1].CreatedAtUtc, page[^1].Id)
            : null;

        return new ListSubmissionsResult(items, nextCursor);
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
                submission.Reference,
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
                quote.ExpiresAtUtc,
                quote.Version,
                quote.SupersedesQuoteId,
                quote.AssuranceStatus,
                quote.EvidenceRequiredCount,
                quote.EvidenceSatisfiedCount
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

        IReadOnlyList<SubmissionControlAssertionResult> latestQuoteAssertions = latestQuote is null
            ? []
            : await dbContext.ControlAssertions
                .AsNoTracking()
                .Where(assertion => assertion.QuoteId == latestQuote.Id)
                .OrderBy(assertion => assertion.ControlType)
                .Select(assertion => new SubmissionControlAssertionResult(
                    assertion.ControlType.ToString(),
                    assertion.ClaimedState,
                    assertion.AssuranceState.ToString(),
                    assertion.EvidenceRequired,
                    assertion.EvidenceReason,
                    assertion.DetailsJson))
                .ToListAsync(cancellationToken);

        return new SubmissionDetailResult(
                submission.Id,
                submission.Reference,
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
                        latestQuote.ExpiresAtUtc,
                        latestQuote.Version,
                        latestQuote.SupersedesQuoteId,
                        latestQuote.AssuranceStatus.ToString(),
                        latestQuote.EvidenceRequiredCount,
                        latestQuote.EvidenceSatisfiedCount,
                        latestQuoteAssertions),
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

    public Task<Guid?> FindMatchingDraftIdAsync(
        string ownerUserId,
        string applicantName,
        string applicantEmail,
        string companyName,
        CancellationToken cancellationToken)
    {
        var normalizedApplicantName = applicantName.Trim();
        var normalizedApplicantEmail = applicantEmail.Trim();
        var normalizedCompanyName = companyName.Trim();

        return dbContext.Submissions
            .AsNoTracking()
            .Where(submission => submission.OwnerUserId == ownerUserId)
            .Where(submission => submission.Status == SubmissionStatus.Draft)
            .Where(submission => submission.ApplicantName == normalizedApplicantName)
            .Where(submission => submission.ApplicantEmail == normalizedApplicantEmail)
            .Where(submission => submission.CompanyName == normalizedCompanyName)
            .OrderByDescending(submission => submission.CreatedAtUtc)
            .Select(submission => (Guid?)submission.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static List<string> SplitLines(string value)
    {
        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string EncodeCursor(DateTime createdAtUtc, Guid submissionId)
    {
        var json = JsonSerializer.Serialize(new SubmissionCursor(createdAtUtc, submissionId));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static SubmissionCursor DecodeCursor(string cursor)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return JsonSerializer.Deserialize<SubmissionCursor>(json)
                ?? throw new FormatException();
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new ArgumentException("Submission cursor is invalid.", nameof(cursor), exception);
        }
    }

    private sealed record SubmissionCursor(DateTime CreatedAtUtc, Guid SubmissionId);
}
