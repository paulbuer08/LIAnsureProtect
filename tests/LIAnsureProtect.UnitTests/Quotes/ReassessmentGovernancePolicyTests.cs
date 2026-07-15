using LIAnsureProtect.Application.Quotes.Commands.CreateQuote;

namespace LIAnsureProtect.UnitTests.Quotes;

public sealed class ReassessmentGovernancePolicyTests
{
    private static readonly DateTime NowUtc = new(2026, 7, 16, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Original_Quote_Does_Not_Start_Reassessment_Cooldown()
    {
        var remaining = ReassessmentGovernancePolicy.GetCooldownRemaining(
            latestQuoteVersion: 1,
            latestQuoteCreatedAtUtc: NowUtc.AddMinutes(-1),
            nowUtc: NowUtc);

        Assert.Null(remaining);
    }

    [Fact]
    public void Successful_Reassessment_Starts_Thirty_Minute_Cooldown()
    {
        var remaining = ReassessmentGovernancePolicy.GetCooldownRemaining(
            latestQuoteVersion: 2,
            latestQuoteCreatedAtUtc: NowUtc.AddMinutes(-10),
            nowUtc: NowUtc);

        Assert.Equal(TimeSpan.FromMinutes(20), remaining);
    }

    [Fact]
    public void Expired_Reassessment_Cooldown_Returns_No_Remaining_Time()
    {
        var remaining = ReassessmentGovernancePolicy.GetCooldownRemaining(
            latestQuoteVersion: 3,
            latestQuoteCreatedAtUtc: NowUtc.AddMinutes(-30),
            nowUtc: NowUtc);

        Assert.Null(remaining);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 1, false)]
    [InlineData(2, 2, true)]
    [InlineData(1, 5, true)]
    public void Manual_Review_Depends_On_Allowance_Not_Cooldown(
        int successfulInRollingWindow,
        int successfulLifetime,
        bool expected)
    {
        Assert.Equal(
            expected,
            ReassessmentGovernancePolicy.RequiresManualReview(
                successfulInRollingWindow,
                successfulLifetime));
    }
}
