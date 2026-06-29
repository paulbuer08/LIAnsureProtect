using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Platform.Abstractions.Security;
using LIAnsureProtect.Application.Policies.Binding;
using LIAnsureProtect.Application.Quotes;
using LIAnsureProtect.Domain.Policies;
using MediatR;

namespace LIAnsureProtect.Application.Policies.Commands.BindPolicy;

public sealed class BindPolicyCommandHandler(
    IQuoteRepository quoteRepository,
    IPolicyRepository policyRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IPolicyBindingProviderClient bindingProviderClient)
    : IRequestHandler<BindPolicyCommand, BindPolicyResult?>
{
    public async Task<BindPolicyResult?> Handle(
        BindPolicyCommand request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = GetRequiredCurrentUserId();
        var quote = await quoteRepository.GetOwnedForBindingAsync(
            request.QuoteId,
            ownerUserId,
            cancellationToken);

        if (quote is null)
            return null;

        if (await policyRepository.ExistsForQuoteAsync(quote.Id, cancellationToken))
            throw new InvalidOperationException("Quote is already bound to a policy.");

        var boundAtUtc = DateTime.UtcNow;
        var policy = Policy.BindFromAcceptedQuote(
            quote,
            PolicyNumberGenerator.Create(boundAtUtc),
            ownerUserId,
            request.EffectiveDateUtc,
            boundAtUtc);
        quote.MarkBound(boundAtUtc);

        var providerRequest = new PolicyBindingProviderRequest(
            policy.Id,
            policy.PolicyNumber,
            policy.QuoteId,
            policy.SubmissionId,
            policy.OwnerUserId,
            policy.Premium,
            policy.RequestedLimit,
            policy.Retention,
            policy.EffectiveDateUtc,
            policy.ExpirationDateUtc);
        var providerResult = await bindingProviderClient.BindAsync(
            providerRequest,
            cancellationToken);
        var attemptCreatedAtUtc = DateTime.UtcNow;
        var attempt = providerResult.Succeeded
            ? PolicyBindingAttempt.Succeeded(
                policy.Id,
                providerResult.ProviderName,
                providerResult.BindingReference
                    ?? throw new InvalidOperationException("A successful binding result must include a binding reference."),
                attemptCreatedAtUtc,
                providerResult.CompletedAtUtc)
            : PolicyBindingAttempt.Failed(
                policy.Id,
                providerResult.ProviderName,
                providerResult.FailureReason
                    ?? "Binding provider returned a failed result.",
                attemptCreatedAtUtc,
                providerResult.CompletedAtUtc);

        await policyRepository.AddAsync(policy, cancellationToken);
        await policyRepository.AddBindingAttemptAsync(attempt, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (!providerResult.Succeeded)
            throw new InvalidOperationException("Policy binding provider did not acknowledge the bind.");

        return new BindPolicyResult(
            policy.Id,
            policy.PolicyNumber,
            policy.QuoteId,
            policy.SubmissionId,
            policy.Status.ToString(),
            policy.Premium,
            policy.RequestedLimit,
            policy.Retention,
            policy.EffectiveDateUtc,
            policy.ExpirationDateUtc,
            policy.BoundByUserId,
            policy.BoundAtUtc,
            providerResult.ProviderName,
            providerResult.BindingReference ?? string.Empty);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to bind a policy.")
            : currentUser.UserId;
    }
}
