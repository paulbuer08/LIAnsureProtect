using LIAnsureProtect.Domain.Submissions;

namespace LIAnsureProtect.UnitTests.Submissions;

public sealed class SubmissionTests
{
    [Fact]
    public void CreateDraft_creates_submission_with_draft_status()
    {
        var createdAtUtc = new DateTime(2026, 6, 8, 10, 30, 0, DateTimeKind.Utc);

        var submission = Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "auth0|owner-user-1",
            createdAtUtc);

        Assert.NotEqual(Guid.Empty, submission.Id);
        Assert.Equal("auth0|owner-user-1", submission.OwnerUserId);
        Assert.Equal("Jane Applicant", submission.ApplicantName);
        Assert.Equal("jane@example.com", submission.ApplicantEmail);
        Assert.Equal("Example Company", submission.CompanyName);
        Assert.Equal(SubmissionStatus.Draft, submission.Status);
        Assert.Equal(createdAtUtc, submission.CreatedAtUtc);
    }

    [Fact]
    public void Submit_changes_draft_submission_to_submitted()
    {
        var submission = CreateDraftSubmission();

        submission.Submit();

        Assert.Equal(SubmissionStatus.Submitted, submission.Status);
    }

    [Fact]
    public void Submit_rejects_non_draft_submission()
    {
        var submission = CreateDraftSubmission();
        submission.Submit();

        var exception = Assert.Throws<InvalidOperationException>(submission.Submit);

        Assert.Equal("Only draft submissions can be submitted.", exception.Message);
    }

    [Fact]
    public void Withdraw_changes_submission_to_withdrawn()
    {
        var submission = CreateDraftSubmission();

        submission.Withdraw();

        Assert.Equal(SubmissionStatus.Withdrawn, submission.Status);
    }

    [Fact]
    public void Withdraw_keeps_withdrawn_submission_withdrawn()
    {
        var submission = CreateDraftSubmission();
        submission.Withdraw();

        submission.Withdraw();

        Assert.Equal(SubmissionStatus.Withdrawn, submission.Status);
    }

    private static Submission CreateDraftSubmission()
    {
        return Submission.CreateDraft(
            "Jane Applicant",
            "jane@example.com",
            "Example Company",
            "auth0|owner-user-1",
            DateTime.UtcNow);
    }
}
