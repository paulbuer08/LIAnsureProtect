using FluentValidation;

namespace LIAnsureProtect.Modules.Quoting.Application.ReferralDecisions;

public sealed class DeclineQuoteReferralCommandValidator : AbstractValidator<DeclineQuoteReferralCommand>
{
    public DeclineQuoteReferralCommandValidator()
    {
        RuleFor(command => command.QuoteId)
            .NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(2_000);

        RuleFor(command => command.Notes)
            .MaximumLength(4_000);
    }
}
