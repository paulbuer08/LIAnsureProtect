namespace LIAnsureProtect.Application.Quotes.Rating;

public sealed class BaselineCyberRatingStrategy : ICyberRatingStrategy
{
    public bool CanRate(CyberRatingInput input)
    {
        return true;
    }

    public CyberRatingResult Rate(CyberRatingInput input)
    {
        var riskTier = CyberRatingMath.CalculateRiskTier(input);
        var referralReasons = CyberRatingMath.BuildReferralReasons(input, riskTier);

        return new CyberRatingResult(
            CyberRatingMath.CalculateTechnicalPremium(input),
            riskTier,
            CyberRatingMath.BuildSubjectivities(input),
            referralReasons,
            "BaselineCyber");
    }
}
