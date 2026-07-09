using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Application.Submissions.Commands.UpdateSubmission;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.UnitTests.Submissions;
using Moq;

namespace LIAnsureProtect.UnitTests.Submissions.UpdateSubmission;

public sealed class UpdateSubmissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_updates_owned_draft_submission_and_saves_changes()
    {
        var submission = CreateDraftSubmission("auth0|owner-user-1");
        var repository = new Mock<ISubmissionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        repository
            .Setup(repo => repo.GetOwnedForUpdateAsync(
                submission.Id,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);
        unitOfWork
            .Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new UpdateSubmissionCommandHandler(
            repository.Object,
            unitOfWork.Object,
            new TestCurrentUser("auth0|owner-user-1"));
        var command = new UpdateSubmissionCommand(
            submission.Id,
            "Updated Applicant",
            "updated@example.com",
            "Updated Company");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(submission.Id, result.SubmissionId);
        Assert.Equal("Updated Applicant", result.ApplicantName);
        Assert.Equal("updated@example.com", result.ApplicantEmail);
        Assert.Equal("Updated Company", result.CompanyName);
        Assert.Equal(nameof(SubmissionStatus.Draft), result.Status);
        Assert.Equal("Updated Applicant", submission.ApplicantName);
        Assert.Equal("updated@example.com", submission.ApplicantEmail);
        Assert.Equal("Updated Company", submission.CompanyName);
        unitOfWork.Verify(
            work => work.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_returns_null_when_submission_is_missing_or_not_owned()
    {
        var repository = new Mock<ISubmissionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var submissionId = Guid.Parse("13e9d8ab-eaaa-418c-9b47-b4d9e3deff3f");
        repository
            .Setup(repo => repo.GetOwnedForUpdateAsync(
                submissionId,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Submission?)null);

        var handler = new UpdateSubmissionCommandHandler(
            repository.Object,
            unitOfWork.Object,
            new TestCurrentUser("auth0|owner-user-1"));
        var command = new UpdateSubmissionCommand(
            submissionId,
            "Updated Applicant",
            "updated@example.com",
            "Updated Company");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Null(result);
        unitOfWork.Verify(
            work => work.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_rejects_submitted_submission_and_does_not_save_changes()
    {
        var submission = CreateDraftSubmission("auth0|owner-user-1");
        submission.Submit();
        var repository = new Mock<ISubmissionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        repository
            .Setup(repo => repo.GetOwnedForUpdateAsync(
                submission.Id,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);

        var handler = new UpdateSubmissionCommandHandler(
            repository.Object,
            unitOfWork.Object,
            new TestCurrentUser("auth0|owner-user-1"));
        var command = new UpdateSubmissionCommand(
            submission.Id,
            "Updated Applicant",
            "updated@example.com",
            "Updated Company");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(command, CancellationToken.None));

        Assert.Equal("Only draft submissions can be edited.", exception.Message);
        unitOfWork.Verify(
            work => work.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Submission CreateDraftSubmission(string ownerUserId)
    {
        return Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            ownerUserId,
            new DateTime(2026, 6, 19, 10, 30, 0, DateTimeKind.Utc));
    }
}
