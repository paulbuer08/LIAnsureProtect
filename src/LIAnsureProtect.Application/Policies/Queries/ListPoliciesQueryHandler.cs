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

        var results = policies.Select(policy => PolicyResultMapper.Map(policy, asOfUtc));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            if (request.Search.Trim().Length > 200)
                throw new ArgumentException("Search text must not exceed 200 characters.", nameof(request));
            var search = request.Search.Trim();
            results = results.Where(policy =>
                policy.PolicyNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                || policy.PolicyId.ToString().Equals(search, StringComparison.OrdinalIgnoreCase)
                || policy.SubmissionReference.Contains(search, StringComparison.OrdinalIgnoreCase)
                || policy.SubmissionId.ToString().Equals(search, StringComparison.OrdinalIgnoreCase)
                || policy.ApplicantName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || policy.CompanyName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(request.ContractualStatus))
            results = results.Where(policy => policy.ContractualStatus.Equals(request.ContractualStatus, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.CoverageState))
            results = results.Where(policy => policy.CoverageState.Equals(request.CoverageState, StringComparison.OrdinalIgnoreCase));

        return new ListPoliciesResult(results.ToList());
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to list policies.")
            : currentUser.UserId;
    }
}
