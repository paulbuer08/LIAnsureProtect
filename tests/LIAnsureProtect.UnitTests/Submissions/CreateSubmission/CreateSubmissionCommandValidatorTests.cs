using LIAnsureProtect.Application.Submissions.Commands.CreateSubmission;

namespace LIAnsureProtect.UnitTests.Submissions.CreateSubmission;

public sealed class CreateSubmissionCommandValidatorTests
{
    [Fact]
    public void Validate_accepts_valid_command()
    {
        var validator = new CreateSubmissionCommandValidator();
        var command = new CreateSubmissionCommand(
            "Jane Applicant",
            "jane@example.com",
            "Example Company");

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_rejects_missing_required_fields()
    {
        var validator = new CreateSubmissionCommandValidator();
        var command = new CreateSubmissionCommand(
            string.Empty,
            string.Empty,
            string.Empty);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateSubmissionCommand.ApplicantName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateSubmissionCommand.ApplicantEmail));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateSubmissionCommand.CompanyName));
    }

    [Fact]
    public void Validate_rejects_invalid_email()
    {
        var validator = new CreateSubmissionCommandValidator();
        var command = new CreateSubmissionCommand(
            "Jane Applicant",
            "not-an-email",
            "Example Company");

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateSubmissionCommand.ApplicantEmail));
    }
}
