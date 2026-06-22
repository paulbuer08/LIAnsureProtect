using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.UnitTests.Quotes;

public sealed class QuoteReferralOperationTests
{
    [Fact]
    public void CreateDefault_sets_realistic_priority_and_sla_for_severe_referral()
    {
        var referredAtUtc = new DateTime(2026, 6, 22, 8, 0, 0, DateTimeKind.Utc);
        var expiresAtUtc = referredAtUtc.AddDays(30);

        var operation = QuoteReferralOperation.CreateDefault(
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            CyberRiskTier.Severe,
            referredAtUtc,
            expiresAtUtc);

        Assert.Equal(ReferralOperationStatus.New, operation.Status);
        Assert.Equal(ReferralPriority.High, operation.Priority);
        Assert.Equal(referredAtUtc.AddDays(2), operation.DueAtUtc);
        Assert.Null(operation.AssignedUnderwriterUserId);
        Assert.Contains(
            operation.TimelineEntries,
            entry => entry.EntryType == ReferralTimelineEntryType.OperationCreated
                && entry.Summary.Contains("High priority", StringComparison.Ordinal));
    }

    [Fact]
    public void AddNote_records_append_only_note_and_timeline_evidence()
    {
        var operation = CreateOperation();
        var notedAtUtc = new DateTime(2026, 6, 22, 9, 0, 0, DateTimeKind.Utc);

        operation.AddNote(
            "underwriter-1",
            "Reviewed MFA evidence and requested EDR rollout confirmation.",
            notedAtUtc);

        var note = Assert.Single(operation.Notes);
        Assert.Equal("Reviewed MFA evidence and requested EDR rollout confirmation.", note.Note);
        Assert.Equal("underwriter-1", note.CreatedByUserId);
        Assert.Contains(
            operation.TimelineEntries,
            entry => entry.EntryType == ReferralTimelineEntryType.NoteAdded
                && entry.CreatedByUserId == "underwriter-1");
    }

    [Fact]
    public void AddTask_and_complete_task_records_operational_timeline()
    {
        var operation = CreateOperation();
        var createdAtUtc = new DateTime(2026, 6, 22, 9, 15, 0, DateTimeKind.Utc);
        var dueAtUtc = new DateTime(2026, 6, 23, 9, 15, 0, DateTimeKind.Utc);
        var completedAtUtc = new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc);

        var task = operation.AddTask(
            "underwriter-1",
            "Verify MFA screenshots",
            dueAtUtc,
            createdAtUtc);

        operation.CompleteTask(task.Id, "underwriter-2", completedAtUtc);

        Assert.True(task.IsCompleted);
        Assert.Equal("underwriter-2", task.CompletedByUserId);
        Assert.Contains(
            operation.TimelineEntries,
            entry => entry.EntryType == ReferralTimelineEntryType.TaskAdded
                && entry.Summary.Contains("Verify MFA screenshots", StringComparison.Ordinal));
        Assert.Contains(
            operation.TimelineEntries,
            entry => entry.EntryType == ReferralTimelineEntryType.TaskCompleted
                && entry.CreatedByUserId == "underwriter-2");
    }

    [Fact]
    public void CloseForDecision_closes_operation_and_blocks_later_mutation()
    {
        var operation = CreateOperation();
        var closedAtUtc = new DateTime(2026, 6, 22, 10, 30, 0, DateTimeKind.Utc);

        operation.CloseForDecision(
            "underwriter-1",
            QuoteUnderwritingDecision.Approved,
            closedAtUtc);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            operation.AddNote(
                "underwriter-1",
                "Trying to add note after final decision.",
                closedAtUtc.AddMinutes(5)));

        Assert.Equal(ReferralOperationStatus.Closed, operation.Status);
        Assert.Equal(closedAtUtc, operation.ClosedAtUtc);
        Assert.Equal("Referral operations are closed.", exception.Message);
        Assert.Contains(
            operation.TimelineEntries,
            entry => entry.EntryType == ReferralTimelineEntryType.StatusChanged
                && entry.Summary.Contains("Approved", StringComparison.Ordinal));
    }

    private static QuoteReferralOperation CreateOperation()
    {
        return QuoteReferralOperation.CreateDefault(
            Guid.Parse("8cfa936a-37a9-4048-8fb9-16a71fc5776b"),
            CyberRiskTier.High,
            new DateTime(2026, 6, 22, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 22, 8, 0, 0, DateTimeKind.Utc));
    }
}
