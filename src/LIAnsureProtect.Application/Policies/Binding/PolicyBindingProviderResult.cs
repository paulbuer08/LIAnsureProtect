namespace LIAnsureProtect.Application.Policies.Binding;

public sealed record PolicyBindingProviderResult(
    string ProviderName,
    bool Succeeded,
    string? BindingReference,
    string? FailureReason,
    DateTime CompletedAtUtc)
{
    public static PolicyBindingProviderResult Success(
        string providerName,
        string bindingReference,
        DateTime completedAtUtc)
    {
        return new PolicyBindingProviderResult(
            providerName,
            Succeeded: true,
            bindingReference,
            FailureReason: null,
            completedAtUtc);
    }

    public static PolicyBindingProviderResult Failure(
        string providerName,
        string failureReason,
        DateTime completedAtUtc)
    {
        return new PolicyBindingProviderResult(
            providerName,
            Succeeded: false,
            BindingReference: null,
            failureReason,
            completedAtUtc);
    }
}
