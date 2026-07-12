using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Application.Quotes.Assurance;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.CreateQuote;

public sealed record CreateQuoteCommand(
    Guid SubmissionId,
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
    string? PriorCyberIncidentDetails = null,
    bool AttestationAccepted = false,
    string? AttestedByName = null,
    string? AttestedByTitle = null,
    bool IsReassessment = false,
    CyberControlDetails? ControlDetails = null) : IRequest<CreateQuoteResult?>;
