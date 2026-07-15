using FluentValidation;
using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes.Commands.CreateQuote;

public sealed class CreateQuoteCommandValidator : AbstractValidator<CreateQuoteCommand>
{
    private static readonly decimal[] SupportedLimits =
    [
        250_000m,
        500_000m,
        1_000_000m,
        2_000_000m,
        5_000_000m
    ];

    private static readonly decimal[] SupportedRetentions =
    [
        2_500m,
        5_000m,
        10_000m,
        25_000m
    ];

    public CreateQuoteCommandValidator()
    {
        RuleFor(command => command.SubmissionId)
            .NotEmpty();

        RuleFor(command => command.RequestedLimit)
            .Must(limit => SupportedLimits.Contains(limit))
            .WithMessage("Requested limit must be one of the supported cyber limits: 250000, 500000, 1000000, 2000000, or 5000000.");

        RuleFor(command => command.Retention)
            .Must(retention => SupportedRetentions.Contains(retention))
            .WithMessage("Retention must be one of the supported cyber retentions: 2500, 5000, 10000, or 25000.");

        RuleFor(command => command.PriorCyberIncidents)
            .InclusiveBetween(0, 5);

        RuleFor(command => command.OtherIndustryDescription)
            .MaximumLength(200);

        RuleFor(command => command.PriorCyberIncidentDetails)
            .MaximumLength(2_000);

        RuleFor(command => command.AttestationAccepted)
            .Equal(true)
            .WithMessage("You must confirm the control attestation before a quote can be generated.");

        RuleFor(command => command.AttestedByName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.AttestedByTitle)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.ControlDetails)
            .NotNull()
            .WithMessage("Detailed control implementation answers are required.");

        When(command => command.IsReassessment, () =>
        {
            RuleFor(command => command.BaseQuoteVersion)
                .NotNull()
                .GreaterThanOrEqualTo(1)
                .WithMessage("The current quote version is required for reassessment.");
        });

        When(command => command.ControlDetails is not null, () =>
        {
            RuleFor(command => command.ControlDetails!.EdrCoveragePercent)
                .InclusiveBetween(0, 100);
            RuleFor(command => command.ControlDetails!.RecoveryPointObjectiveHours)
                .InclusiveBetween(0, 720);
            RuleFor(command => command.ControlDetails!.RecoveryTimeObjectiveHours)
                .InclusiveBetween(0, 720);
            RuleFor(command => command.ControlDetails!.SensitiveDataTypes)
                .Must(types => types is null || types.All(type =>
                    !string.IsNullOrWhiteSpace(type) && type.Length <= 100))
                .WithMessage("Sensitive data types must be non-empty and no more than 100 characters each.");
            RuleFor(command => command.ControlDetails!.SensitiveDataVolume)
                .MaximumLength(100);

            When(command => command.MfaStatus == CyberSecurityControlStatus.Implemented, () =>
            {
                RuleFor(command => command.ControlDetails!)
                    .Must(details => details.MfaCoversPrivilegedAccess
                        && details.MfaCoversEmail
                        && details.MfaCoversRemoteAccess)
                    .WithMessage("Implemented MFA must cover privileged access, email, and remote access.");
            });
            When(command => command.EdrStatus == CyberSecurityControlStatus.Implemented, () =>
            {
                RuleFor(command => command.ControlDetails!)
                    .Must(details => details.EdrCoveragePercent >= 90
                        && details.EdrCoversServers
                        && details.EdrActivelyMonitored
                        && details.EdrTamperProtection)
                    .WithMessage("Implemented EDR requires at least 90% coverage, servers, active monitoring, and tamper protection.");
            });
            When(command => command.BackupMaturity == BackupMaturity.Mature, () =>
            {
                RuleFor(command => command.ControlDetails!)
                    .Must(details => details.BackupsImmutableOrOffline
                        && details.BackupCredentialsSeparated
                        && details.RestoreTestedLast12Months)
                    .WithMessage("Mature backups require immutable/offline copies, separate credentials, and a recent restore test.");
            });
            When(command => command.HasIncidentResponsePlan, () =>
            {
                RuleFor(command => command.ControlDetails!)
                    .Must(details => details.IncidentPlanApproved
                        && details.IncidentPlanUpdatedLast12Months
                        && details.IncidentPlanTestedLast12Months
                        && details.IncidentRolesNamed)
                    .WithMessage("An in-place incident plan must be approved, current, tested, and assign named roles.");
            });
            When(command => command.SensitiveDataExposure == SensitiveDataExposure.Low, () =>
            {
                RuleFor(command => command.ControlDetails!)
                    .Must(details => details.SensitiveDataInventoryMaintained && details.SensitiveDataEncrypted)
                    .WithMessage("Low sensitive-data exposure requires a maintained inventory and encryption support.");
            });
        });

        RuleFor(command => command.PriorCyberIncidentTypes)
            .Must(types => types is null || types.All(type => !string.IsNullOrWhiteSpace(type) && type.Length <= 100))
            .WithMessage("Prior cyber incident types must be non-empty and no more than 100 characters each.");

        When(command => command.PriorCyberIncidents > 0, () =>
        {
            RuleFor(command => command.PriorCyberIncidentTypes)
                .NotEmpty()
                .WithMessage("Prior cyber incident types are required when prior cyber incidents are greater than zero.");

            RuleFor(command => command.PriorCyberIncidentDetails)
                .NotEmpty()
                .WithMessage("Prior cyber incident details are required when prior cyber incidents are greater than zero.");
        });
    }
}
