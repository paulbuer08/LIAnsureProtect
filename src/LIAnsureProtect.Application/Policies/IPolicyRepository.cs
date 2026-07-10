using LIAnsureProtect.Domain.Policies;
using LIAnsureProtect.Application.Policies.Queries;

namespace LIAnsureProtect.Application.Policies;

public interface IPolicyRepository
{
    Task AddAsync(Policy policy, CancellationToken cancellationToken);

    Task AddBindingAttemptAsync(
        PolicyBindingAttempt attempt,
        CancellationToken cancellationToken);

    Task<bool> ExistsForQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PolicyReadModel>> ListOwnedAsync(
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<PolicyReadModel?> GetOwnedAsync(
        Guid policyId,
        string ownerUserId,
        CancellationToken cancellationToken);
}
