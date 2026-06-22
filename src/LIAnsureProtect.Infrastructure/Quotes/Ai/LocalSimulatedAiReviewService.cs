using LIAnsureProtect.Application.Quotes.Ai;

namespace LIAnsureProtect.Infrastructure.Quotes.Ai;

public sealed class LocalSimulatedAiReviewService : IAiReviewService
{
    private const string ProviderName = "Local Simulated AI";

    public Task<AiReviewProviderResult> GenerateUnderwritingReviewAsync(
        AiReviewProviderRequest request,
        CancellationToken cancellationToken)
    {
        var completedAtUtc = DateTime.UtcNow;
        var primaryReferralReason = request.ReferralReasons.FirstOrDefault()
            ?? "The quote was referred for underwriter review.";
        var primarySubjectivity = request.Subjectivities.FirstOrDefault()
            ?? "No subjectivities were supplied in the quote context.";

        return Task.FromResult(AiReviewProviderResult.Succeeded(
            ProviderName,
            $"Advisory review for a {request.RiskTier} cyber quote referred because: {primaryReferralReason}",
            [
                "The quote has a complete local rating and referral context available for human review.",
                $"The requested limit is {request.RequestedLimit:C0} with retention {request.Retention:C0}."
            ],
            [
                $"The quote remains in {request.Status} status and requires a human underwriting decision.",
                $"Open subjectivity to validate: {primarySubjectivity}"
            ],
            [
                "Identity and access management evidence should be confirmed before relying on MFA-related controls.",
                "Endpoint detection, backup maturity, incident response, and sensitive data exposure should be checked against the submitted risk answers."
            ],
            [
                "Can the applicant provide current MFA evidence for privileged and remote access?",
                "Can the applicant confirm EDR deployment coverage and alert monitoring ownership?",
                "When was the last successful backup restore test completed?",
                "Are incident response roles, external counsel, and breach response contacts documented?"
            ],
            [
                primarySubjectivity,
                "Evidence of backup restore testing may be required before bind.",
                "Incident response plan evidence may be required for higher-limit risks."
            ],
            [
                "quote.riskTier",
                "quote.referralReasons",
                "quote.subjectivities",
                "quote.premiumLimitRetention",
                "quote.strategyName"
            ],
            [
                "No uploaded documents, external scans, broker emails, or embeddings were reviewed in this milestone.",
                "The assistant cannot verify whether applicant-provided controls are operating effectively."
            ],
            AiReviewConstants.AdvisoryDisclaimer,
            completedAtUtc));
    }
}
