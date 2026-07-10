using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Application.Policies.Queries;

public sealed class ListPoliciesQueryHandler(
    IPolicyRepository policyRepository,
    ICurrentUser currentUser) : IRequestHandler<ListPoliciesQuery, ListPoliciesResult>
{
    public async Task<ListPoliciesResult> Handle(
        ListPoliciesQuery request,
        CancellationToken cancellationToken)
    {
        var policies = await policyRepository.ListOwnedAsync(
            GetRequiredCurrentUserId(),
            cancellationToken);
        var asOfUtc = DateTime.UtcNow;

        return new ListPoliciesResult(
            policies.Select(policy => PolicyResultMapper.Map(policy, asOfUtc)).ToList());
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to list policies.")
            : currentUser.UserId;
    }
}
