using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Application.Submissions.Commands.CreateSubmission;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.UnitTests.Submissions;
using Moq;

namespace LIAnsureProtect.UnitTests.Submissions.CreateSubmission;

public sealed class CreateSubmissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_creates_draft_submission_and_saves_it()
    {
        Submission? savedSubmission = null;

        var repository = new Mock<ISubmissionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        repository
            .Setup(repo => repo.FindMatchingDraftIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        repository
            .Setup(repo => repo.AddAsync(
                It.IsAny<Submission>(),
                It.IsAny<CancellationToken>()))
            .Callback<Submission, CancellationToken>((submission, _) =>
            {
                savedSubmission = submission;
            })
            .Returns(Task.CompletedTask);
        unitOfWork
            .Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var currentUser = new TestCurrentUser("auth0|owner-user-1");
        var handler = new CreateSubmissionCommandHandler(
            repository.Object,
            unitOfWork.Object,
            currentUser);
        var command = new CreateSubmissionCommand(
            "Jane Applicant",
            "jane@example.com",
            "Example Company");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.SubmissionId);
        Assert.Equal(nameof(SubmissionStatus.Draft), result.Status);
        Assert.False(result.ExistingDraft);
        Assert.NotNull(savedSubmission);
        Assert.Equal(result.SubmissionId, savedSubmission.Id);
        Assert.Equal("auth0|owner-user-1", savedSubmission.OwnerUserId);
        Assert.Equal("Jane Applicant", savedSubmission.ApplicantName);
        Assert.Equal("jane@example.com", savedSubmission.ApplicantEmail);
        Assert.Equal("Example Company", savedSubmission.CompanyName);
        Assert.Equal(SubmissionStatus.Draft, savedSubmission.Status);

        repository.Verify(
            repo => repo.AddAsync(
                It.IsAny<Submission>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        unitOfWork.Verify(
            work => work.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_returns_matching_draft_without_creating_another_one()
    {
        var existingDraftId = Guid.NewGuid();
        var repository = new Mock<ISubmissionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        repository
            .Setup(repo => repo.FindMatchingDraftIdAsync(
                "auth0|owner-user-1",
                "Jane Applicant",
                "jane@example.com",
                "Example Company",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDraftId);

        var handler = new CreateSubmissionCommandHandler(
            repository.Object,
            unitOfWork.Object,
            new TestCurrentUser("auth0|owner-user-1"));

        var result = await handler.Handle(
            new CreateSubmissionCommand(
                "Jane Applicant",
                "jane@example.com",
                "Example Company"),
            CancellationToken.None);

        Assert.Equal(existingDraftId, result.SubmissionId);
        Assert.Equal(nameof(SubmissionStatus.Draft), result.Status);
        Assert.True(result.PossibleDuplicate);
        Assert.True(result.ExistingDraft);
        repository.Verify(
            repo => repo.AddAsync(It.IsAny<Submission>(), It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWork.Verify(
            work => work.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
