using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes.RatingProviders;

public sealed record RatingProviderRequest(
    Guid QuoteId,
    Guid SubmissionId,
    string OwnerUserId,
    CyberIndustryClass IndustryClass,
    AnnualRevenueBand AnnualRevenueBand,
    decimal RequestedLimit,
    decimal Retention,
    CyberSecurityControlStatus MfaStatus,
    CyberSecurityControlStatus EdrStatus,
    BackupMaturity BackupMaturity,
    bool HasIncidentResponsePlan,
    int PriorCyberIncidents,
    SensitiveDataExposure SensitiveDataExposure,
    string? OtherIndustryDescription,
    IReadOnlyCollection<string>? PriorCyberIncidentTypes,
    string? PriorCyberIncidentDetails,
    decimal LocalPremium,
    CyberRiskTier LocalRiskTier,
    QuoteStatus LocalStatus,
    string LocalRatingStrategyName);
