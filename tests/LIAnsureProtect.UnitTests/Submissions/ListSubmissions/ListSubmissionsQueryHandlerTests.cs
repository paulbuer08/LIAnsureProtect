using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;
using LIAnsureProtect.UnitTests.Submissions;
using Moq;

namespace LIAnsureProtect.UnitTests.Submissions.ListSubmissions;

public sealed class ListSubmissionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_returns_submission_list_from_repository()
    {
        var createdAtUtc = new DateTime(2026, 6, 19, 8, 30, 0, DateTimeKind.Utc);
        var expectedSubmissions = new List<SubmissionListItemResult>
        {
            new(
                Guid.Parse("af1453a4-0b68-4432-99d9-becb456a1001"),
                "Jane Applicant",
                "jane@example.com",
                "Example Company",
                "Draft",
                createdAtUtc)
        };

        var repository = new Mock<ISubmissionRepository>();
        repository
            .Setup(repo => repo.ListAsync(
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSubmissions);

        var currentUser = new TestCurrentUser("auth0|owner-user-1");
        var handler = new ListSubmissionsQueryHandler(repository.Object, currentUser);

        var result = await handler.Handle(new ListSubmissionsQuery(), CancellationToken.None);

        var submission = Assert.Single(result.Submissions);
        Assert.Equal(expectedSubmissions[0].SubmissionId, submission.SubmissionId);
        Assert.Equal("Jane Applicant", submission.ApplicantName);
        Assert.Equal("jane@example.com", submission.ApplicantEmail);
        Assert.Equal("Example Company", submission.CompanyName);
        Assert.Equal("Draft", submission.Status);
        Assert.Equal(createdAtUtc, submission.CreatedAtUtc);
        repository.Verify(
            repo => repo.ListAsync(
                "auth0|owner-user-1",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
