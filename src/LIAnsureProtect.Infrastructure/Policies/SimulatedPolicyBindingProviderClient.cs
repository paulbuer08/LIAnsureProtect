using LIAnsureProtect.Application.Policies.Binding;

namespace LIAnsureProtect.Infrastructure.Policies;

public sealed class SimulatedPolicyBindingProviderClient : IPolicyBindingProviderClient
{
    public Task<PolicyBindingProviderResult> BindAsync(
        PolicyBindingProviderRequest request,
        CancellationToken cancellationToken)
    {
        var reference = $"LIP-SIM-BIND-{request.PolicyNumber[^8..]}";

        return Task.FromResult(PolicyBindingProviderResult.Success(
            providerName: "LIAnsureProtect Simulated Binding Desk",
            bindingReference: reference,
            completedAtUtc: DateTime.UtcNow));
    }
}
