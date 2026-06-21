using LIAnsureProtect.Domain.Quotes;
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
    SensitiveDataExposure SensitiveDataExposure) : IRequest<CreateQuoteResult?>;
