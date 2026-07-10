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
    public void Submit_records_submission_submitted_domain_event()
    {
        var submission = CreateDraftSubmission();

        submission.Submit();

        var domainEvent = Assert.Single(
            submission.DomainEvents.OfType<SubmissionSubmittedDomainEvent>());
        Assert.Equal(submission.Id, domainEvent.SubmissionId);
        Assert.Equal(submission.OwnerUserId, domainEvent.OwnerUserId);
    }

    [Fact]
    public void ClearDomainEvents_removes_recorded_domain_events()
    {
        var submission = CreateDraftSubmission();
        submission.Submit();

        submission.ClearDomainEvents();

        Assert.Empty(submission.DomainEvents);
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
    public void UpdateDraftDetails_changes_editable_intake_fields()
    {
        var submission = CreateDraftSubmission();

        submission.UpdateDraftDetails(
            "Updated Applicant",
            "updated@example.com",
            "Updated Company");

        Assert.Equal("Updated Applicant", submission.ApplicantName);
        Assert.Equal("updated@example.com", submission.ApplicantEmail);
        Assert.Equal("Updated Company", submission.CompanyName);
        Assert.Equal(SubmissionStatus.Draft, submission.Status);
    }

    [Fact]
    public void UpdateDraftDetails_rejects_non_draft_submission()
    {
        var submission = CreateDraftSubmission();
        submission.Submit();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            submission.UpdateDraftDetails(
                "Updated Applicant",
                "updated@example.com",
                "Updated Company"));

        Assert.Equal("Only draft submissions can be edited.", exception.Message);
    }

    [Fact]
    public void Withdraw_changes_submission_to_withdrawn()
    {
        var submission = CreateDraftSubmission();
        submission.Submit();
        submission.ClearDomainEvents();
        var withdrawnAtUtc = new DateTime(2026, 7, 11, 1, 0, 0, DateTimeKind.Utc);

        submission.Withdraw(withdrawnAtUtc);

        Assert.Equal(SubmissionStatus.Withdrawn, submission.Status);
        var domainEvent = Assert.IsType<SubmissionWithdrawnDomainEvent>(
            Assert.Single(submission.DomainEvents));
        Assert.Equal(withdrawnAtUtc, domainEvent.OccurredAtUtc);
    }

    [Fact]
    public void Withdraw_keeps_withdrawn_submission_withdrawn()
    {
        var submission = CreateDraftSubmission();
        submission.Submit();
        submission.ClearDomainEvents();
        submission.Withdraw(DateTime.UtcNow);

        submission.Withdraw(DateTime.UtcNow.AddMinutes(1));

        Assert.Equal(SubmissionStatus.Withdrawn, submission.Status);
        Assert.Single(submission.DomainEvents);
    }

    [Fact]
    public void Withdraw_rejects_a_draft_that_should_be_deleted_instead()
    {
        var submission = CreateDraftSubmission();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            submission.Withdraw(DateTime.UtcNow));

        Assert.Contains("Delete an unsubmitted draft", exception.Message);
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
