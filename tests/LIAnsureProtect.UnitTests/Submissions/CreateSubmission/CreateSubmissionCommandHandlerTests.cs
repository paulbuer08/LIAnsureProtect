using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Application.Submissions.Commands.CreateSubmission;
using LIAnsureProtect.Domain.Submissions;
using Moq;

namespace LIAnsureProtect.UnitTests.Submissions.CreateSubmission;

public sealed class CreateSubmissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_creates_draft_submission_and_saves_it()
    {
        Submission? savedSubmission = null;

        var repository = new Mock<ISubmissionRepository>();
        repository
            .Setup(repo => repo.AddAsync(
                It.IsAny<Submission>(),
                It.IsAny<CancellationToken>()))
            .Callback<Submission, CancellationToken>((submission, _) =>
            {
                savedSubmission = submission;
            })
            .Returns(Task.CompletedTask);

        var handler = new CreateSubmissionCommandHandler(repository.Object);
        var command = new CreateSubmissionCommand(
            "Jane Applicant",
            "jane@example.com",
            "Example Company");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.SubmissionId);
        Assert.Equal(nameof(SubmissionStatus.Draft), result.Status);
        Assert.NotNull(savedSubmission);
        Assert.Equal(result.SubmissionId, savedSubmission.Id);
        Assert.Equal("Jane Applicant", savedSubmission.ApplicantName);
        Assert.Equal("jane@example.com", savedSubmission.ApplicantEmail);
        Assert.Equal("Example Company", savedSubmission.CompanyName);
        Assert.Equal(SubmissionStatus.Draft, savedSubmission.Status);

        repository.Verify(
            repo => repo.AddAsync(
                It.IsAny<Submission>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
