using FluentValidation;

namespace LIAnsureProtect.Application.Submissions.Commands.CreateSubmission;

public sealed class CreateSubmissionCommandValidator : AbstractValidator<CreateSubmissionCommand>
{
    public CreateSubmissionCommandValidator()
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
