using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;
using LIAnsureProtect.UnitTests.Submissions;
using Moq;

namespace LIAnsureProtect.UnitTests.Submissions.GetSubmissionDetail;

public sealed class GetSubmissionDetailQueryHandlerTests
{
    [Fact]
    public async Task Handle_returns_submission_detail_from_repository()
    {
        var submissionId = Guid.Parse("af1453a4-0b68-4432-99d9-becb456a1001");
        var createdAtUtc = new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc);
        var expectedDetail = new SubmissionDetailResult(
            submissionId,
            "SUB-2026-AF1453A40B684432",
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "Draft",
            createdAtUtc);

        var repository = new Mock<ISubmissionRepository>();
        repository
            .Setup(repo => repo.GetDetailAsync(
                submissionId,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var currentUser = new TestCurrentUser("auth0|owner-user-1");
        var handler = new GetSubmissionDetailQueryHandler(repository.Object, currentUser);

        var result = await handler.Handle(
            new GetSubmissionDetailQuery(submissionId),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(submissionId, result.SubmissionId);
        Assert.Equal("Jane Applicant", result.ApplicantName);
        Assert.Equal("jane@example.com", result.ApplicantEmail);
        Assert.Equal("Example Company", result.CompanyName);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(createdAtUtc, result.CreatedAtUtc);
        repository.Verify(
            repo => repo.GetDetailAsync(
                submissionId,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_returns_null_when_repository_does_not_find_submission()
    {
        var submissionId = Guid.Parse("c43e4434-6b30-4d52-a38b-b2d24f8a1002");

        var repository = new Mock<ISubmissionRepository>();
        repository
            .Setup(repo => repo.GetDetailAsync(
                submissionId,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubmissionDetailResult?)null);

        var currentUser = new TestCurrentUser("auth0|owner-user-1");
        var handler = new GetSubmissionDetailQueryHandler(repository.Object, currentUser);

        var result = await handler.Handle(
            new GetSubmissionDetailQuery(submissionId),
            CancellationToken.None);

        Assert.Null(result);
        repository.Verify(
            repo => repo.GetDetailAsync(
                submissionId,
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
