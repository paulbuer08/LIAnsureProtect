using LIAnsureProtect.Application.Policies;
using LIAnsureProtect.Domain.Policies;
using LIAnsureProtect.Application.Policies.Queries;
using LIAnsureProtect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Policies;

public sealed class EfCorePolicyRepository(SubmissionDbContext dbContext) : IPolicyRepository
{
    public async Task AddAsync(Policy policy, CancellationToken cancellationToken)
    {
        await dbContext.Policies.AddAsync(policy, cancellationToken);
    }

    public async Task AddBindingAttemptAsync(
        PolicyBindingAttempt attempt,
        CancellationToken cancellationToken)
    {
        await dbContext.PolicyBindingAttempts.AddAsync(attempt, cancellationToken);
    }

    public async Task<bool> ExistsForQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Policies.AnyAsync(
            policy => policy.QuoteId == quoteId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PolicyReadModel>> ListOwnedAsync(
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var policies = await (
            from policy in dbContext.Policies.AsNoTracking()
            join submission in dbContext.Submissions.AsNoTracking()
                on policy.SubmissionId equals submission.Id
            where policy.OwnerUserId == ownerUserId
                && submission.OwnerUserId == ownerUserId
            orderby policy.EffectiveDateUtc descending
            select new
            {
                policy.Id,
                policy.PolicyNumber,
                policy.Status,
                policy.EffectiveDateUtc,
                policy.ExpirationDateUtc,
                policy.Premium,
                policy.RequestedLimit,
                policy.Retention,
                policy.QuoteId,
                policy.SubmissionId,
                policy.QuoteStatusAtBind,
                policy.QuoteRiskTierAtBind,
                policy.QuoteSubjectivitiesAtBind,
                submission.ApplicantName,
                submission.CompanyName
            })
            .ToListAsync(cancellationToken);

        return policies.Select(policy => new PolicyReadModel(
            policy.Id,
            policy.PolicyNumber,
            policy.Status.ToString(),
            policy.EffectiveDateUtc,
            policy.ExpirationDateUtc,
            policy.Premium,
            policy.RequestedLimit,
            policy.Retention,
            policy.QuoteId,
            policy.SubmissionId,
            policy.QuoteStatusAtBind,
            policy.QuoteRiskTierAtBind,
            policy.QuoteSubjectivitiesAtBind,
            policy.ApplicantName,
            policy.CompanyName)).ToList();
    }

    public async Task<PolicyReadModel?> GetOwnedAsync(
        Guid policyId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var policyRow = await (
            from policy in dbContext.Policies.AsNoTracking()
            join submission in dbContext.Submissions.AsNoTracking()
                on policy.SubmissionId equals submission.Id
            where policy.Id == policyId
                && policy.OwnerUserId == ownerUserId
                && submission.OwnerUserId == ownerUserId
            select new
            {
                policy.Id,
                policy.PolicyNumber,
                policy.Status,
                policy.EffectiveDateUtc,
                policy.ExpirationDateUtc,
                policy.Premium,
                policy.RequestedLimit,
                policy.Retention,
                policy.QuoteId,
                policy.SubmissionId,
                policy.QuoteStatusAtBind,
                policy.QuoteRiskTierAtBind,
                policy.QuoteSubjectivitiesAtBind,
                submission.ApplicantName,
                submission.CompanyName
            }).SingleOrDefaultAsync(cancellationToken);

        return policyRow is null ? null : new PolicyReadModel(
            policyRow.Id,
            policyRow.PolicyNumber,
            policyRow.Status.ToString(),
            policyRow.EffectiveDateUtc,
            policyRow.ExpirationDateUtc,
            policyRow.Premium,
            policyRow.RequestedLimit,
            policyRow.Retention,
            policyRow.QuoteId,
            policyRow.SubmissionId,
            policyRow.QuoteStatusAtBind,
            policyRow.QuoteRiskTierAtBind,
            policyRow.QuoteSubjectivitiesAtBind,
            policyRow.ApplicantName,
            policyRow.CompanyName);
    }
}
