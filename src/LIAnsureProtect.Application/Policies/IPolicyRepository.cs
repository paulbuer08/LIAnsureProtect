using LIAnsureProtect.Domain.Policies;

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
}
