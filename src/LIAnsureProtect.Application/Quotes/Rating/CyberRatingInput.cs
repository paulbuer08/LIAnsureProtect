using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes.Rating;

public sealed record CyberRatingInput(
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
    string? OtherIndustryDescription = null,
    IReadOnlyCollection<string>? PriorCyberIncidentTypes = null,
    string? PriorCyberIncidentDetails = null);
