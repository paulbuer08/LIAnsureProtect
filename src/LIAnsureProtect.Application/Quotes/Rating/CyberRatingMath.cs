using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes.Rating;

internal static class CyberRatingMath
{
    public static decimal CalculateTechnicalPremium(CyberRatingInput input, decimal extraLoading = 1.0m)
    {
        var premium = GetBasePremium(input.AnnualRevenueBand)
            * GetIndustryFactor(input.IndustryClass)
            * GetLimitFactor(input.RequestedLimit)
            * GetRetentionFactor(input.Retention)
            * GetControlFactor(input.MfaStatus, implementedFactor: 0.90m, partialFactor: 1.05m, missingFactor: 1.25m)
            * GetControlFactor(input.EdrStatus, implementedFactor: 0.92m, partialFactor: 1.08m, missingFactor: 1.20m)
            * GetBackupFactor(input.BackupMaturity)
            * (input.HasIncidentResponsePlan ? 0.95m : 1.15m)
            * GetPriorIncidentFactor(input.PriorCyberIncidents)
            * GetSensitiveDataFactor(input.SensitiveDataExposure)
            * extraLoading;

        return Math.Round(Math.Max(1_000m, premium), 2, MidpointRounding.AwayFromZero);
    }

    public static CyberRiskTier CalculateRiskTier(CyberRatingInput input)
    {
        var score = 0;

        score += input.IndustryClass switch
        {
            CyberIndustryClass.Healthcare => 2,
            CyberIndustryClass.FinancialServices => 2,
            CyberIndustryClass.Retail => 1,
            _ => 0
        };
        score += HasUnclassifiedIndustry(input) ? 1 : 0;

        score += input.AnnualRevenueBand switch
        {
            AnnualRevenueBand.From50MTo250M => 2,
            AnnualRevenueBand.From10MTo50M => 1,
            _ => 0
        };
        score += input.RequestedLimit >= 5_000_000m ? 2 : input.RequestedLimit >= 2_000_000m ? 1 : 0;
        score += input.Retention <= 2_500m ? 1 : 0;
        score += GetControlScore(input.MfaStatus);
        score += GetControlScore(input.EdrStatus);
        score += input.BackupMaturity switch
        {
            BackupMaturity.Weak => 2,
            BackupMaturity.Partial => 1,
            _ => 0
        };
        score += input.HasIncidentResponsePlan ? 0 : 1;
        score += Math.Min(input.PriorCyberIncidents * 2, 4);
        score += HasSevereIncidentHistory(input) ? 2 : 0;
        score += input.SensitiveDataExposure switch
        {
            SensitiveDataExposure.High => 2,
            SensitiveDataExposure.Moderate => 1,
            _ => 0
        };

        return score switch
        {
            >= 11 => CyberRiskTier.Severe,
            >= 7 => CyberRiskTier.High,
            >= 2 => CyberRiskTier.Moderate,
            _ => CyberRiskTier.Low
        };
    }

    public static IReadOnlyList<string> BuildSubjectivities(CyberRatingInput input)
    {
        var subjectivities = new List<string>
        {
            input.MfaStatus == CyberSecurityControlStatus.Implemented
                ? "MFA is implemented"
                : "MFA is not implemented",
            input.EdrStatus == CyberSecurityControlStatus.Implemented
                ? "EDR is implemented"
                : "Endpoint detection and response requires follow-up",
            input.BackupMaturity == BackupMaturity.Mature
                ? "Backup program is mature"
                : "Backup maturity requires underwriter review"
        };

        if (!input.HasIncidentResponsePlan)
            subjectivities.Add("Incident response plan is missing.");

        if (input.SensitiveDataExposure == SensitiveDataExposure.High)
            subjectivities.Add("High sensitive data exposure requires security-control evidence.");

        if (HasUnclassifiedIndustry(input))
            subjectivities.Add("Other industry description requires underwriting classification confirmation.");

        if (input.PriorCyberIncidents > 0)
            subjectivities.Add("Prior incident history requires a loss-history review.");

        return subjectivities;
    }

    public static IReadOnlyList<string> BuildReferralReasons(CyberRatingInput input, CyberRiskTier riskTier)
    {
        var referralReasons = new List<string>();

        if (riskTier == CyberRiskTier.Severe)
            referralReasons.Add("Severe risk tier requires underwriter review.");

        if (input.PriorCyberIncidents >= 2)
            referralReasons.Add("Prior cyber incident count requires underwriter review.");

        if (HasSevereIncidentHistory(input))
            referralReasons.Add("Severe prior incident type requires underwriter review.");

        if (input.PriorCyberIncidents > 0
            && string.IsNullOrWhiteSpace(input.PriorCyberIncidentDetails))
        {
            referralReasons.Add("Prior incident details are required for underwriting review.");
        }

        if (HasUnclassifiedIndustry(input))
            referralReasons.Add("Other industry class requires underwriter classification review.");

        if (input.MfaStatus == CyberSecurityControlStatus.NotImplemented)
            referralReasons.Add("Missing MFA requires underwriter review.");

        if (input.BackupMaturity == BackupMaturity.Weak)
            referralReasons.Add("Weak backup controls require underwriter review.");

        if (input.SensitiveDataExposure == SensitiveDataExposure.High
            && (input.IndustryClass == CyberIndustryClass.Healthcare
                || input.IndustryClass == CyberIndustryClass.FinancialServices))
        {
            referralReasons.Add("High sensitive-data exposure in a regulated industry requires underwriter review.");
        }

        return referralReasons.Distinct().ToList();
    }

    private static decimal GetBasePremium(AnnualRevenueBand annualRevenueBand)
    {
        return annualRevenueBand switch
        {
            AnnualRevenueBand.Under1M => 1_200m,
            AnnualRevenueBand.From1MTo10M => 2_500m,
            AnnualRevenueBand.From10MTo50M => 7_500m,
            AnnualRevenueBand.From50MTo250M => 18_000m,
            _ => throw new ArgumentOutOfRangeException(nameof(annualRevenueBand))
        };
    }

    private static decimal GetIndustryFactor(CyberIndustryClass industryClass)
    {
        return industryClass switch
        {
            CyberIndustryClass.ProfessionalServices => 1.00m,
            CyberIndustryClass.Technology => 1.15m,
            CyberIndustryClass.Retail => 1.20m,
            CyberIndustryClass.Healthcare => 1.35m,
            CyberIndustryClass.FinancialServices => 1.45m,
            _ => throw new ArgumentOutOfRangeException(nameof(industryClass))
        };
    }

    private static decimal GetLimitFactor(decimal requestedLimit)
    {
        return requestedLimit switch
        {
            <= 250_000m => 0.65m,
            <= 500_000m => 0.80m,
            <= 1_000_000m => 1.00m,
            <= 2_000_000m => 1.65m,
            <= 5_000_000m => 3.25m,
            _ => 4.50m
        };
    }

    private static decimal GetRetentionFactor(decimal retention)
    {
        return retention switch
        {
            <= 2_500m => 1.15m,
            <= 5_000m => 1.05m,
            <= 10_000m => 1.00m,
            <= 25_000m => 0.85m,
            _ => 0.80m
        };
    }

    private static decimal GetControlFactor(
        CyberSecurityControlStatus status,
        decimal implementedFactor,
        decimal partialFactor,
        decimal missingFactor)
    {
        return status switch
        {
            CyberSecurityControlStatus.Implemented => implementedFactor,
            CyberSecurityControlStatus.Partial => partialFactor,
            CyberSecurityControlStatus.NotImplemented => missingFactor,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    private static decimal GetBackupFactor(BackupMaturity backupMaturity)
    {
        return backupMaturity switch
        {
            BackupMaturity.Mature => 0.92m,
            BackupMaturity.Partial => 1.08m,
            BackupMaturity.Weak => 1.20m,
            _ => throw new ArgumentOutOfRangeException(nameof(backupMaturity))
        };
    }

    private static decimal GetPriorIncidentFactor(int priorCyberIncidents)
    {
        return priorCyberIncidents switch
        {
            <= 0 => 1.00m,
            1 => 1.25m,
            2 => 1.55m,
            _ => 1.85m
        };
    }

    private static decimal GetSensitiveDataFactor(SensitiveDataExposure sensitiveDataExposure)
    {
        return sensitiveDataExposure switch
        {
            SensitiveDataExposure.Low => 1.00m,
            SensitiveDataExposure.Moderate => 1.10m,
            SensitiveDataExposure.High => 1.25m,
            _ => throw new ArgumentOutOfRangeException(nameof(sensitiveDataExposure))
        };
    }

    private static bool HasUnclassifiedIndustry(CyberRatingInput input)
    {
        return !string.IsNullOrWhiteSpace(input.OtherIndustryDescription);
    }

    private static bool HasSevereIncidentHistory(CyberRatingInput input)
    {
        var incidentTypes = input.PriorCyberIncidentTypes ?? [];

        return incidentTypes.Any(type =>
            type.Contains("ransomware", StringComparison.OrdinalIgnoreCase)
            || type.Contains("data breach", StringComparison.OrdinalIgnoreCase)
            || type.Contains("funds transfer fraud", StringComparison.OrdinalIgnoreCase)
            || type.Contains("business email compromise", StringComparison.OrdinalIgnoreCase));
    }

    private static int GetControlScore(CyberSecurityControlStatus status)
    {
        return status switch
        {
            CyberSecurityControlStatus.Implemented => 0,
            CyberSecurityControlStatus.Partial => 1,
            CyberSecurityControlStatus.NotImplemented => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }
}
