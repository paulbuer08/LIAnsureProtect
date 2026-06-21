using FluentValidation;

namespace LIAnsureProtect.Application.Quotes.Commands.UnderwriteQuoteReferral;

public sealed class AdjustQuoteReferralCommandValidator : AbstractValidator<AdjustQuoteReferralCommand>
{
    public AdjustQuoteReferralCommandValidator()
    {
        RuleFor(command => command.QuoteId)
            .NotEmpty();

        RuleFor(command => command.AdjustedPremium)
            .GreaterThan(0);

        RuleFor(command => command.AdjustedRetention)
            .GreaterThan(0);

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(2_000);

        RuleFor(command => command.Notes)
            .MaximumLength(4_000);

        RuleFor(command => command.UpdatedSubjectivities)
            .MaximumLength(4_000);
    }
}
