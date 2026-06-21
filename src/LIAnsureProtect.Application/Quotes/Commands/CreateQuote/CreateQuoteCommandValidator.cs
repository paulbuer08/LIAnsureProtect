using FluentValidation;

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
    }
}
