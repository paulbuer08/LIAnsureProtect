using LIAnsureProtect.Application.Quotes.Rating;
using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.UnitTests.Quotes;

public sealed class CyberRatingStrategyTests
{
    [Fact]
    public void Baseline_strategy_calculates_lower_premium_for_stronger_controls()
    {
        var strategy = new BaselineCyberRatingStrategy();
        var weakControlInput = CreateInput(
            mfaStatus: CyberSecurityControlStatus.NotImplemented,
            edrStatus: CyberSecurityControlStatus.Partial,
            backupMaturity: BackupMaturity.Weak,
            hasIncidentResponsePlan: false);
        var strongControlInput = CreateInput(
            mfaStatus: CyberSecurityControlStatus.Implemented,
            edrStatus: CyberSecurityControlStatus.Implemented,
            backupMaturity: BackupMaturity.Mature,
            hasIncidentResponsePlan: true);

        var weakControlResult = strategy.Rate(weakControlInput);
        var strongControlResult = strategy.Rate(strongControlInput);

        Assert.True(weakControlResult.Premium > strongControlResult.Premium);
        Assert.Contains("MFA is implemented", strongControlResult.Subjectivities);
        Assert.Contains("MFA is not implemented", weakControlResult.Subjectivities);
    }

    [Fact]
    public void Selector_uses_high_risk_strategy_for_severe_cyber_profile()
    {
        var selector = new CyberRatingStrategySelector(
        [
            new HighRiskCyberRatingStrategy(),
            new BaselineCyberRatingStrategy()
        ]);
        var input = CreateInput(
            industryClass: CyberIndustryClass.Healthcare,
            requestedLimit: 5_000_000m,
            retention: 2_500m,
            mfaStatus: CyberSecurityControlStatus.NotImplemented,
            edrStatus: CyberSecurityControlStatus.NotImplemented,
            backupMaturity: BackupMaturity.Weak,
            hasIncidentResponsePlan: false,
            priorCyberIncidents: 2,
            sensitiveDataExposure: SensitiveDataExposure.High);

        var result = selector.Rate(input);

        Assert.Equal("HighRiskCyber", result.StrategyName);
        Assert.Equal(CyberRiskTier.Severe, result.RiskTier);
        Assert.NotEmpty(result.ReferralReasons);
        Assert.Contains("Prior cyber incident count requires underwriter review.", result.ReferralReasons);
    }

    [Fact]
    public void Selector_refers_other_industry_for_underwriter_classification()
    {
        var selector = new CyberRatingStrategySelector(
        [
            new HighRiskCyberRatingStrategy(),
            new BaselineCyberRatingStrategy()
        ]);
        var input = CreateInput(otherIndustryDescription: "Marine logistics and port security");

        var result = selector.Rate(input);

        Assert.Equal("HighRiskCyber", result.StrategyName);
        Assert.Contains(
            "Other industry class requires underwriter classification review.",
            result.ReferralReasons);
        Assert.Contains(
            "Other industry description requires underwriting classification confirmation.",
            result.Subjectivities);
    }

    [Fact]
    public void Selector_refers_severe_prior_incident_type_even_when_count_is_one()
    {
        var selector = new CyberRatingStrategySelector(
        [
            new HighRiskCyberRatingStrategy(),
            new BaselineCyberRatingStrategy()
        ]);
        var input = CreateInput(
            priorCyberIncidents: 1,
            priorCyberIncidentTypes: ["Ransomware"],
            priorCyberIncidentDetails: "Ransomware incident affected file servers last year; backups were restored.");

        var result = selector.Rate(input);

        Assert.Equal("HighRiskCyber", result.StrategyName);
        Assert.Contains("Severe prior incident type requires underwriter review.", result.ReferralReasons);
        Assert.Contains("Prior incident history requires a loss-history review.", result.Subjectivities);
    }

    private static CyberRatingInput CreateInput(
        CyberIndustryClass industryClass = CyberIndustryClass.ProfessionalServices,
        AnnualRevenueBand annualRevenueBand = AnnualRevenueBand.From10MTo50M,
        decimal requestedLimit = 1_000_000m,
        decimal retention = 10_000m,
        CyberSecurityControlStatus mfaStatus = CyberSecurityControlStatus.Implemented,
        CyberSecurityControlStatus edrStatus = CyberSecurityControlStatus.Implemented,
        BackupMaturity backupMaturity = BackupMaturity.Mature,
        bool hasIncidentResponsePlan = true,
        int priorCyberIncidents = 0,
        SensitiveDataExposure sensitiveDataExposure = SensitiveDataExposure.Moderate,
        string? otherIndustryDescription = null,
        IReadOnlyCollection<string>? priorCyberIncidentTypes = null,
        string? priorCyberIncidentDetails = null)
    {
        return new CyberRatingInput(
            industryClass,
            annualRevenueBand,
            requestedLimit,
            retention,
            mfaStatus,
            edrStatus,
            backupMaturity,
            hasIncidentResponsePlan,
            priorCyberIncidents,
            sensitiveDataExposure,
            otherIndustryDescription,
            priorCyberIncidentTypes,
            priorCyberIncidentDetails);
    }
}
