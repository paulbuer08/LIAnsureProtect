using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes.Rating;

public sealed class HighRiskCyberRatingStrategy : ICyberRatingStrategy
{
    public bool CanRate(CyberRatingInput input)
    {
        return input.PriorCyberIncidents >= 2
            || input.MfaStatus == CyberSecurityControlStatus.NotImplemented
            || input.BackupMaturity == BackupMaturity.Weak
            || (input.SensitiveDataExposure == SensitiveDataExposure.High
                && (input.IndustryClass == CyberIndustryClass.Healthcare
                    || input.IndustryClass == CyberIndustryClass.FinancialServices));
    }

    public CyberRatingResult Rate(CyberRatingInput input)
    {
        var riskTier = CyberRatingMath.CalculateRiskTier(input);
        if (riskTier < CyberRiskTier.High)
            riskTier = CyberRiskTier.High;

        var referralReasons = CyberRatingMath.BuildReferralReasons(input, riskTier).ToList();
        if (referralReasons.Count == 0)
            referralReasons.Add("High-risk cyber profile requires underwriter review.");

        return new CyberRatingResult(
            CyberRatingMath.CalculateTechnicalPremium(input, extraLoading: 1.25m),
            riskTier,
            CyberRatingMath.BuildSubjectivities(input),
            referralReasons,
            "HighRiskCyber");
    }
}
