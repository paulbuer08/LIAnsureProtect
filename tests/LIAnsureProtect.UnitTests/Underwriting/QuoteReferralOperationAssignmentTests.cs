using LIAnsureProtect.Modules.Underwriting.Domain.Referrals;

namespace LIAnsureProtect.UnitTests.Underwriting;

/// <summary>
/// Referral assignment is a claim, not a note: once one underwriter holds it, a second claim must
/// be rejected at the domain (same-user re-clicks stay idempotent), and every mutation bumps the
/// optimistic-concurrency <c>Version</c> so racing writers are caught at save time.
/// </summary>
public sealed class QuoteReferralOperationAssignmentTests
{
    private static QuoteReferralOperation CreateOperation()
    {
        var referredAtUtc = new DateTime(2026, 7, 3, 8, 0, 0, DateTimeKind.Utc);
        return QuoteReferralOperation.CreateDefault(
            Guid.NewGuid(),
            "High",
            referredAtUtc,
            referredAtUtc.AddDays(30));
    }

    [Fact]
    public void AssignTo_Rejects_A_Second_Underwriter_When_Already_Assigned()
    {
        var operation = CreateOperation();
        operation.AssignTo("underwriter-1", DateTime.UtcNow);

        var exception = Assert.Throws<InvalidOperationException>(
            () => operation.AssignTo("underwriter-2", DateTime.UtcNow));

        Assert.Contains("already assigned", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("underwriter-1", operation.AssignedUnderwriterUserId);
    }

    [Fact]
    public void AssignTo_Is_Idempotent_For_The_Same_Underwriter()
    {
        var operation = CreateOperation();
        operation.AssignTo("underwriter-1", DateTime.UtcNow);
        var timelineCountAfterFirstAssign = operation.TimelineEntries.Count;

        operation.AssignTo("underwriter-1", DateTime.UtcNow);

        Assert.Equal("underwriter-1", operation.AssignedUnderwriterUserId);
        Assert.Equal(timelineCountAfterFirstAssign, operation.TimelineEntries.Count);
    }

    [Fact]
    public void AssignTo_Allows_A_New_Underwriter_After_Release()
    {
        var operation = CreateOperation();
        operation.AssignTo("underwriter-1", DateTime.UtcNow);
        operation.ReleaseAssignment("underwriter-1", DateTime.UtcNow);

        operation.AssignTo("underwriter-2", DateTime.UtcNow);

        Assert.Equal("underwriter-2", operation.AssignedUnderwriterUserId);
    }

    [Fact]
    public void Mutations_Increment_The_Concurrency_Version()
    {
        var operation = CreateOperation();
        var initialVersion = operation.Version;

        operation.AssignTo("underwriter-1", DateTime.UtcNow);
        Assert.Equal(initialVersion + 1, operation.Version);

        operation.ReleaseAssignment("underwriter-1", DateTime.UtcNow);
        Assert.Equal(initialVersion + 2, operation.Version);
    }
}
