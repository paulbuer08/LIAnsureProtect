using FluentValidation;

namespace LIAnsureProtect.Application.Quotes.Commands.UnderwriteQuoteReferral;

public sealed class ApproveQuoteReferralCommandValidator : AbstractValidator<ApproveQuoteReferralCommand>
{
    public ApproveQuoteReferralCommandValidator()
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
