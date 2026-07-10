using LIAnsureProtect.Application.Policies.Queries;
using System.Globalization;

namespace LIAnsureProtect.UnitTests.Policies;

public sealed class PolicyCoverageStateTests
{
    private static readonly DateTime EffectiveAt = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ExpiresAt = new(2027, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("2026-06-30T23:59:59Z", "Scheduled")]
    [InlineData("2026-07-01T00:00:00Z", "Active")]
    [InlineData("2027-06-30T23:59:59Z", "Active")]
    [InlineData("2027-07-01T00:00:00Z", "Expired")]
    public void Compute_Derives_Coverage_Without_Rewriting_Contractual_Status(
        string asOfUtc,
        string expected)
    {
        var result = PolicyCoverageState.Compute(
            "Bound",
            EffectiveAt,
            ExpiresAt,
            DateTime.Parse(asOfUtc, CultureInfo.InvariantCulture).ToUniversalTime());

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Compute_Preserves_Explicit_Cancelled_State()
    {
        var result = PolicyCoverageState.Compute(
            "Cancelled",
            EffectiveAt,
            ExpiresAt,
            EffectiveAt.AddDays(1));

        Assert.Equal("Cancelled", result);
    }
}
