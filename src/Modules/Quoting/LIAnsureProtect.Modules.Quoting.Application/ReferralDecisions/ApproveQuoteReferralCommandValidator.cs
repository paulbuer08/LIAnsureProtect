using FluentValidation;

namespace LIAnsureProtect.Modules.Quoting.Application.ReferralDecisions;

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
