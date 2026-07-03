using LIAnsureProtect.Modules.Claims.Domain;

namespace LIAnsureProtect.UnitTests.Modules.Claims;

public sealed class ClaimFinancialsTests
{
    private static readonly DateTime FiledAtUtc = new(2026, 3, 13, 10, 0, 0, DateTimeKind.Utc);

    private static Claim FileClaim()
    {
        var claim = Claim.File(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer-1",
            "CLM-2026-0A1B2C3D",
            ClaimIncidentType.RansomwareExtortion,
            new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 12, 9, 30, 0, DateTimeKind.Utc),
            "Ransomware encrypted the file server.",
            "POL-2026-11111111",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            1_000_000m,
            25_000m,
            FiledAtUtc);
        claim.ClearDomainEvents();

        return claim;
    }

    private static Claim AssignedClaim(string adjuster = "adjuster-1")
    {
        var claim = FileClaim();
        claim.AssignTo(adjuster, FiledAtUtc.AddHours(1));
        claim.ClearDomainEvents();

        return claim;
    }

    // --- Claimed amount (the claimant's declaration) ---

    [Fact]
    public void Claimed_Amount_Starts_Unset_And_Money_Starts_At_Zero()
    {
        var claim = FileClaim();

        Assert.Null(claim.ClaimedAmount);
        Assert.Equal(0m, claim.ReserveAmount);
        Assert.Equal(0m, claim.PaidAmount);
    }

    [Fact]
    public void Owner_Can_Declare_And_Update_The_Claimed_Amount()
    {
        var claim = FileClaim();

        claim.SetClaimedAmount(250_000m, "customer-1", FiledAtUtc.AddHours(1));
        Assert.Equal(250_000m, claim.ClaimedAmount);

        claim.SetClaimedAmount(300_000m, "customer-1", FiledAtUtc.AddHours(2));
        Assert.Equal(300_000m, claim.ClaimedAmount);
        Assert.Equal(2, claim.TimelineEntries.Count(entry => entry.EntryType == ClaimTimelineEntryType.ClaimedAmountUpdated));
    }

    [Fact]
    public void Claimed_Amount_May_Exceed_The_Policy_Limit()
    {
        // The claimant may claim anything; CM5 caps the *settlement*, not the demand.
        var claim = FileClaim();

        claim.SetClaimedAmount(5_000_000m, "customer-1", FiledAtUtc.AddHours(1));

        Assert.Equal(5_000_000m, claim.ClaimedAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_Positive_Claimed_Amounts_Are_Rejected(decimal amount)
    {
        var claim = FileClaim();

        Assert.Throws<ArgumentException>(() => claim.SetClaimedAmount(amount, "customer-1", FiledAtUtc.AddHours(1)));
    }

    [Fact]
    public void Claimed_Amount_Cannot_Change_After_A_Decision()
    {
        var claim = AssignedClaim();
        claim.Accept(100_000m, "Covered loss.", null, "adjuster-1", FiledAtUtc.AddHours(2));

        Assert.Throws<InvalidOperationException>(() => claim.SetClaimedAmount(1m, "customer-1", FiledAtUtc.AddHours(3)));
    }

    // --- Reserve (the assigned adjuster's estimate, append-only history) ---

    [Fact]
    public void Assigned_Adjuster_Sets_The_Reserve_With_A_History_Row()
    {
        var claim = AssignedClaim();

        claim.SetReserve(150_000m, "Initial estimate from forensic scoping call.", "adjuster-1", FiledAtUtc.AddHours(2));

        Assert.Equal(150_000m, claim.ReserveAmount);
        var change = Assert.Single(claim.ReserveChanges);
        Assert.Equal(0m, change.OldAmount);
        Assert.Equal(150_000m, change.NewAmount);
        Assert.Equal("adjuster-1", change.ChangedByUserId);
        Assert.Equal("Initial estimate from forensic scoping call.", change.Reason);
        Assert.Contains(claim.TimelineEntries, entry => entry.EntryType == ClaimTimelineEntryType.ReserveChanged);
    }

    [Fact]
    public void Reserve_Adjustments_Append_History_In_Order()
    {
        var claim = AssignedClaim();
        claim.SetReserve(150_000m, "Initial estimate.", "adjuster-1", FiledAtUtc.AddHours(2));

        claim.SetReserve(90_000m, "Backups recovered; exposure reduced.", "adjuster-1", FiledAtUtc.AddHours(3));

        Assert.Equal(90_000m, claim.ReserveAmount);
        Assert.Equal(2, claim.ReserveChanges.Count);
        var latest = claim.ReserveChanges.OrderBy(change => change.ChangedAtUtc).Last();
        Assert.Equal(150_000m, latest.OldAmount);
        Assert.Equal(90_000m, latest.NewAmount);
    }

    [Fact]
    public void Releasing_The_Reserve_To_Zero_Is_Legal_And_Audited()
    {
        var claim = AssignedClaim();
        claim.SetReserve(150_000m, "Initial estimate.", "adjuster-1", FiledAtUtc.AddHours(2));

        claim.SetReserve(0m, "Claim likely to be denied; reserve released.", "adjuster-1", FiledAtUtc.AddHours(3));

        Assert.Equal(0m, claim.ReserveAmount);
        Assert.Equal(2, claim.ReserveChanges.Count);
    }

    [Fact]
    public void Reserve_Requires_An_Assigned_Adjuster()
    {
        var claim = FileClaim();

        Assert.Throws<InvalidOperationException>(() =>
            claim.SetReserve(150_000m, "Initial estimate.", "adjuster-1", FiledAtUtc.AddHours(1)));
    }

    [Fact]
    public void Only_The_Assigned_Adjuster_Can_Move_The_Reserve()
    {
        var claim = AssignedClaim("adjuster-1");

        Assert.Throws<InvalidOperationException>(() =>
            claim.SetReserve(150_000m, "Second opinion.", "adjuster-2", FiledAtUtc.AddHours(2)));
    }

    [Fact]
    public void Negative_Reserves_Are_Rejected()
    {
        var claim = AssignedClaim();

        Assert.Throws<ArgumentException>(() =>
            claim.SetReserve(-1m, "Nonsense.", "adjuster-1", FiledAtUtc.AddHours(2)));
    }

    [Fact]
    public void A_Reserve_Change_Requires_A_Reason()
    {
        var claim = AssignedClaim();

        Assert.Throws<ArgumentException>(() =>
            claim.SetReserve(150_000m, "  ", "adjuster-1", FiledAtUtc.AddHours(2)));
    }

    [Fact]
    public void Setting_The_Same_Reserve_Amount_Is_Rejected_As_Noise()
    {
        var claim = AssignedClaim();
        claim.SetReserve(150_000m, "Initial estimate.", "adjuster-1", FiledAtUtc.AddHours(2));

        Assert.Throws<InvalidOperationException>(() =>
            claim.SetReserve(150_000m, "Same amount again.", "adjuster-1", FiledAtUtc.AddHours(3)));
    }

    [Fact]
    public void Reserve_Cannot_Change_After_A_Decision()
    {
        var claim = AssignedClaim();
        claim.SetReserve(150_000m, "Initial estimate.", "adjuster-1", FiledAtUtc.AddHours(2));
        claim.Deny(ClaimDenialReason.InsufficientEvidence, "No forensic report provided.", "adjuster-1", FiledAtUtc.AddHours(3));

        Assert.Throws<InvalidOperationException>(() =>
            claim.SetReserve(0m, "Release after denial.", "adjuster-1", FiledAtUtc.AddHours(4)));
    }

    [Fact]
    public void Financial_Mutations_Bump_The_Concurrency_Version()
    {
        var claim = AssignedClaim();
        var versionBefore = claim.Version;

        claim.SetClaimedAmount(250_000m, "customer-1", FiledAtUtc.AddHours(2));
        var versionAfterClaimed = claim.Version;
        Assert.True(versionAfterClaimed > versionBefore);

        claim.SetReserve(150_000m, "Initial estimate.", "adjuster-1", FiledAtUtc.AddHours(3));
        Assert.True(claim.Version > versionAfterClaimed);
    }
}
