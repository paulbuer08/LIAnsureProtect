namespace LIAnsureProtect.Application.Policies.Queries;

internal static class PolicyResultMapper
{
    public static PolicyResult Map(PolicyReadModel policy, DateTime asOfUtc)
    {
        return new PolicyResult(
            policy.PolicyId,
            policy.PolicyNumber,
            policy.ContractualStatus,
            PolicyCoverageState.Compute(
                policy.ContractualStatus,
                policy.EffectiveDateUtc,
                policy.ExpirationDateUtc,
                asOfUtc),
            policy.EffectiveDateUtc,
            policy.ExpirationDateUtc,
            policy.Premium,
            policy.RequestedLimit,
            policy.Retention,
            policy.QuoteId,
            policy.SubmissionId,
            policy.QuoteStatusAtBind,
            policy.QuoteRiskTierAtBind,
            policy.QuoteSubjectivitiesAtBind
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            policy.ApplicantName,
            policy.CompanyName,
            policy.SubmissionReference);
    }
}
