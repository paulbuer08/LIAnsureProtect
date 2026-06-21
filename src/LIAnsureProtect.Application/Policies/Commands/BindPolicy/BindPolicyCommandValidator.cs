using FluentValidation;

namespace LIAnsureProtect.Application.Policies.Commands.BindPolicy;

public sealed class BindPolicyCommandValidator : AbstractValidator<BindPolicyCommand>
{
    public BindPolicyCommandValidator()
    {
        RuleFor(command => command.QuoteId)
            .NotEmpty();

        RuleFor(command => command.EffectiveDateUtc)
            .NotEmpty()
            .WithMessage("Effective date is required.");
    }
}
