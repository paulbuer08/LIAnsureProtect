using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Application.Submissions.Commands.SubmitSubmission;
using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.UnitTests.Submissions;
using Moq;

namespace LIAnsureProtect.UnitTests.Submissions.SubmitSubmission;

public sealed class SubmitSubmissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_submits_owned_draft_submission_and_saves_changes()
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

        var handler = new SubmitSubmissionCommandHandler(
            repository.Object,
            unitOfWork.Object,
            new TestCurrentUser("auth0|owner-user-1"));
        var command = new SubmitSubmissionCommand(submission.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(submission.Id, result.SubmissionId);
        Assert.Equal(nameof(SubmissionStatus.Submitted), result.Status);
        Assert.Equal(SubmissionStatus.Submitted, submission.Status);
        Assert.Contains(
            submission.DomainEvents,
            domainEvent => domainEvent is SubmissionSubmittedDomainEvent);
        unitOfWork.Verify(
            work => work.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_returns_null_when_submission_is_missing_or_not_owned()
    {
        var repository = new Mock<ISubmissionRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var submissionId = Guid.Parse("5a2d5576-4c8e-4f18-9c52-bb3f17264703");
        repository
            .Setup(repo => repo.GetOwnedForUpdateAsync(
                submissionId,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Submission?)null);

        var handler = new SubmitSubmissionCommandHandler(
            repository.Object,
            unitOfWork.Object,
            new TestCurrentUser("auth0|owner-user-1"));
        var command = new SubmitSubmissionCommand(submissionId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Null(result);
        unitOfWork.Verify(
            work => work.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_rejects_repeated_submit_and_does_not_save_changes()
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

        var handler = new SubmitSubmissionCommandHandler(
            repository.Object,
            unitOfWork.Object,
            new TestCurrentUser("auth0|owner-user-1"));
        var command = new SubmitSubmissionCommand(submission.Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(command, CancellationToken.None));

        Assert.Equal("Only draft submissions can be submitted.", exception.Message);
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
