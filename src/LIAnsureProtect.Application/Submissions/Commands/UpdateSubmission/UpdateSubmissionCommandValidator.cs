using FluentValidation;

namespace LIAnsureProtect.Application.Submissions.Commands.UpdateSubmission;

public sealed class UpdateSubmissionCommandValidator : AbstractValidator<UpdateSubmissionCommand>
{
    public UpdateSubmissionCommandValidator()
    {
        RuleFor(command => command.ApplicantName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.ApplicantEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(command => command.CompanyName)
            .NotEmpty()
            .MaximumLength(200);
    }
}
