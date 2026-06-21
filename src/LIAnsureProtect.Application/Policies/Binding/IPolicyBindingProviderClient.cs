namespace LIAnsureProtect.Application.Policies.Binding;

public interface IPolicyBindingProviderClient
{
    Task<PolicyBindingProviderResult> BindAsync(
        PolicyBindingProviderRequest request,
        CancellationToken cancellationToken);
}
