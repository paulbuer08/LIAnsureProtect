namespace LIAnsureProtect.Domain.Policies;

public sealed class PolicyBindingAttempt
{
    private PolicyBindingAttempt(
        Guid id,
        Guid policyId,
        string providerName,
        PolicyBindingAttemptStatus status,
        string? bindingReference,
        string? failureReason,
        DateTime createdAtUtc,
        DateTime completedAtUtc)
    {
        Id = id;
        PolicyId = policyId;
        ProviderName = providerName;
        Status = status;
        BindingReference = bindingReference;
        FailureReason = failureReason;
        CreatedAtUtc = createdAtUtc;
        CompletedAtUtc = completedAtUtc;
    }

    private PolicyBindingAttempt()
    {
        ProviderName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid PolicyId { get; private set; }

    public string ProviderName { get; private set; }

    public PolicyBindingAttemptStatus Status { get; private set; }

    public string? BindingReference { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime CompletedAtUtc { get; private set; }

    public static PolicyBindingAttempt Succeeded(
        Guid policyId,
        string providerName,
        string bindingReference,
        DateTime createdAtUtc,
        DateTime completedAtUtc)
    {
        if (policyId == Guid.Empty)
            throw new ArgumentException("Policy id is required.", nameof(policyId));

        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name is required.", nameof(providerName));

        if (string.IsNullOrWhiteSpace(bindingReference))
            throw new ArgumentException("Binding reference is required.", nameof(bindingReference));

        return new PolicyBindingAttempt(
            Guid.NewGuid(),
            policyId,
            providerName.Trim(),
            PolicyBindingAttemptStatus.Succeeded,
            bindingReference.Trim(),
            failureReason: null,
            createdAtUtc,
            completedAtUtc);
    }

    public static PolicyBindingAttempt Failed(
        Guid policyId,
        string providerName,
        string failureReason,
        DateTime createdAtUtc,
        DateTime completedAtUtc)
    {
        if (policyId == Guid.Empty)
            throw new ArgumentException("Policy id is required.", nameof(policyId));

        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name is required.", nameof(providerName));

        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("Failure reason is required.", nameof(failureReason));

        return new PolicyBindingAttempt(
            Guid.NewGuid(),
            policyId,
            providerName.Trim(),
            PolicyBindingAttemptStatus.Failed,
            bindingReference: null,
            failureReason.Trim(),
            createdAtUtc,
            completedAtUtc);
    }
}
