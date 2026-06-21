using LIAnsureProtect.Application.Policies;
using LIAnsureProtect.Domain.Policies;
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
}
