using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Application.Policies.Queries;

public sealed class GetPolicyQueryHandler(
    IPolicyRepository policyRepository,
    ICurrentUser currentUser) : IRequestHandler<GetPolicyQuery, PolicyResult?>
{
    public async Task<PolicyResult?> Handle(
        GetPolicyQuery request,
        CancellationToken cancellationToken)
    {
        var policy = await policyRepository.GetOwnedAsync(
            request.PolicyId,
            GetRequiredCurrentUserId(),
            cancellationToken);

        return policy is null ? null : PolicyResultMapper.Map(policy, DateTime.UtcNow);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to view a policy.")
            : currentUser.UserId;
    }
}
