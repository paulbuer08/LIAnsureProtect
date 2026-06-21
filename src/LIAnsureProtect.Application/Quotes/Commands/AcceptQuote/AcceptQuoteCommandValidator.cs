using FluentValidation;

namespace LIAnsureProtect.Application.Quotes.Commands.AcceptQuote;

public sealed class AcceptQuoteCommandValidator : AbstractValidator<AcceptQuoteCommand>
{
    public AcceptQuoteCommandValidator()
    {
        RuleFor(command => command.QuoteId)
            .NotEmpty();

        RuleFor(command => command.AcceptedByName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.AcceptedByTitle)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.SubjectivitiesAcknowledged)
            .Equal(true)
            .WithMessage("Quote subjectivities must be acknowledged before acceptance.");
    }
}
