using LIAnsureProtect.Modules.Claims.Domain;

namespace LIAnsureProtect.UnitTests.Modules.Claims;

public sealed class ClaimDecisionTests
{
    private static readonly DateTime FiledAtUtc = new(2026, 3, 13, 10, 0, 0, DateTimeKind.Utc);
    private const decimal Limit = 1_000_000m;
    private const decimal Retention = 25_000m;
    private const decimal Cap = Limit - Retention;

    private static Claim AssignedClaim(string adjuster = "adjuster-1")
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
            Limit,
            Retention,
            FiledAtUtc);
        claim.AssignTo(adjuster, FiledAtUtc.AddHours(1));
        claim.SetClaimedAmount(500_000m, "customer-1", FiledAtUtc.AddHours(1));
        claim.SetReserve(200_000m, "Initial estimate.", adjuster, FiledAtUtc.AddHours(1));
        claim.ClearDomainEvents();

        return claim;
    }

    // --- Accept ---

    [Fact]
    public void Accept_Records_Settlement_Payment_Audit_And_Event()
    {
        var claim = AssignedClaim();

        claim.Accept(300_000m, "Covered ransomware loss; forensic report verified.", "Wire authorized.", "adjuster-1", FiledAtUtc.AddDays(1));

        Assert.Equal(ClaimStatus.Accepted, claim.Status);
        Assert.Equal(300_000m, claim.SettlementAmount);
        Assert.Equal(300_000m, claim.PaidAmount);
        Assert.Equal("adjuster-1", claim.DecidedByUserId);

        var decision = Assert.Single(claim.Decisions);
        Assert.Equal(ClaimDecisionOutcome.Accepted, decision.Outcome);
        Assert.Equal(300_000m, decision.SettlementAmount);
        Assert.Equal(500_000m, decision.ClaimedAmountAtDecision);
        Assert.Equal(200_000m, decision.ReserveAmountAtDecision);
        Assert.Equal("Covered ransomware loss; forensic report verified.", decision.Reason);

        var acceptedEvent = Assert.IsType<ClaimAcceptedDomainEvent>(Assert.Single(claim.DomainEvents));
        Assert.Equal(300_000m, acceptedEvent.SettlementAmount);
    }

    [Fact]
    public void Accept_At_Exactly_The_Cap_Is_Legal()
    {
        var claim = AssignedClaim();

        claim.Accept(Cap, "Total loss; full limit less retention.", null, "adjuster-1", FiledAtUtc.AddDays(1));

        Assert.Equal(Cap, claim.SettlementAmount);
    }

    [Fact]
    public void Accept_Over_The_Cap_Is_Rejected()
    {
        var claim = AssignedClaim();

        Assert.Throws<InvalidOperationException>(() =>
            claim.Accept(Cap + 0.01m, "Too generous.", null, "adjuster-1", FiledAtUtc.AddDays(1)));
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Empty(claim.Decisions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_Positive_Settlements_Are_Rejected(decimal amount)
    {
        var claim = AssignedClaim();

        Assert.Throws<ArgumentException>(() =>
            claim.Accept(amount, "Zero pay.", null, "adjuster-1", FiledAtUtc.AddDays(1)));
    }

    [Fact]
    public void Accept_Requires_A_Reason()
    {
        var claim = AssignedClaim();

        Assert.Throws<ArgumentException>(() =>
            claim.Accept(300_000m, "  ", null, "adjuster-1", FiledAtUtc.AddDays(1)));
    }

    [Fact]
    public void Accept_Requires_The_Assigned_Adjuster()
    {
        var claim = AssignedClaim("adjuster-1");

        Assert.Throws<InvalidOperationException>(() =>
            claim.Accept(300_000m, "Not my file.", null, "adjuster-2", FiledAtUtc.AddDays(1)));
    }

    [Fact]
    public void No_Decision_Without_Assignment()
    {
        var claim = Claim.File(
            Guid.NewGuid(), Guid.NewGuid(), "customer-1", "CLM-2026-0A1B2C3D",
            ClaimIncidentType.Other,
            new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc),
            "Something happened.",
            "POL-2026-11111111",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Limit, Retention, FiledAtUtc);
        // StartReview flips the status without assigning — the claim is UnderReview but unowned.
        claim.StartReview("adjuster-1", FiledAtUtc.AddHours(1));

        Assert.Throws<InvalidOperationException>(() =>
            claim.Accept(1_000m, "Unassigned decision.", null, "adjuster-1", FiledAtUtc.AddDays(1)));
        Assert.Throws<InvalidOperationException>(() =>
            claim.Deny(ClaimDenialReason.NotCovered, "Unassigned denial.", "adjuster-1", FiledAtUtc.AddDays(1)));
    }

    // --- Deny ---

    [Fact]
    public void Deny_Records_Category_Narrative_Audit_And_Event()
    {
        var claim = AssignedClaim();

        claim.Deny(ClaimDenialReason.PolicyExclusion, "War exclusion applies to state-sponsored attacks.", "adjuster-1", FiledAtUtc.AddDays(1));

        Assert.Equal(ClaimStatus.Denied, claim.Status);
        Assert.Equal(ClaimDenialReason.PolicyExclusion, claim.DenialReason);
        Assert.Equal("War exclusion applies to state-sponsored attacks.", claim.DenialNarrative);
        Assert.Equal(0m, claim.PaidAmount);

        var decision = Assert.Single(claim.Decisions);
        Assert.Equal(ClaimDecisionOutcome.Denied, decision.Outcome);
        Assert.Null(decision.SettlementAmount);
        Assert.Equal(ClaimDenialReason.PolicyExclusion, decision.DenialReason);

        var deniedEvent = Assert.IsType<ClaimDeniedDomainEvent>(Assert.Single(claim.DomainEvents));
        Assert.Equal("PolicyExclusion", deniedEvent.DenialReason.ToString());
    }

    [Fact]
    public void Denial_Requires_A_Narrative()
    {
        var claim = AssignedClaim();

        Assert.Throws<ArgumentException>(() =>
            claim.Deny(ClaimDenialReason.NotCovered, "  ", "adjuster-1", FiledAtUtc.AddDays(1)));
    }

    [Fact]
    public void Deny_Requires_The_Assigned_Adjuster()
    {
        var claim = AssignedClaim("adjuster-1");

        Assert.Throws<InvalidOperationException>(() =>
            claim.Deny(ClaimDenialReason.NotCovered, "Not my file.", "adjuster-2", FiledAtUtc.AddDays(1)));
    }

    // --- Close ---

    [Fact]
    public void Close_After_Accept_Writes_Audit_And_Event()
    {
        var claim = AssignedClaim();
        claim.Accept(300_000m, "Covered loss.", null, "adjuster-1", FiledAtUtc.AddDays(1));
        claim.ClearDomainEvents();

        claim.Close("adjuster-1", FiledAtUtc.AddDays(2));

        Assert.Equal(ClaimStatus.Closed, claim.Status);
        Assert.NotNull(claim.ClosedAtUtc);
        Assert.Equal(2, claim.Decisions.Count);
        Assert.Equal(ClaimDecisionOutcome.Closed, claim.Decisions.OrderBy(d => d.DecidedAtUtc).Last().Outcome);
        Assert.IsType<ClaimClosedDomainEvent>(Assert.Single(claim.DomainEvents));
    }

    [Fact]
    public void Close_Releases_The_Outstanding_Reserve_With_An_Audited_Change()
    {
        var claim = AssignedClaim();
        claim.Deny(ClaimDenialReason.InsufficientEvidence, "No forensic report provided.", "adjuster-1", FiledAtUtc.AddDays(1));

        claim.Close("adjuster-1", FiledAtUtc.AddDays(2));

        // The envelope is emptied when the file is finished — and the release is audited.
        Assert.Equal(0m, claim.ReserveAmount);
        var release = claim.ReserveChanges.OrderBy(change => change.ChangedAtUtc).Last();
        Assert.Equal(200_000m, release.OldAmount);
        Assert.Equal(0m, release.NewAmount);
        Assert.Contains("closure", release.Reason, StringComparison.OrdinalIgnoreCase);

        // The Closed audit row snapshots the reserve as it stood at close (pre-release).
        var closedDecision = claim.Decisions.OrderBy(decision => decision.DecidedAtUtc).Last();
        Assert.Equal(200_000m, closedDecision.ReserveAmountAtDecision);
    }

    [Fact]
    public void Close_With_A_Zero_Reserve_Adds_No_Release_Row()
    {
        var claim = AssignedClaim();
        claim.SetReserve(0m, "Claim likely to be denied; reserve released.", "adjuster-1", FiledAtUtc.AddHours(2));
        var reserveChangesBeforeClose = claim.ReserveChanges.Count;
        claim.Deny(ClaimDenialReason.NotCovered, "Not covered.", "adjuster-1", FiledAtUtc.AddDays(1));

        claim.Close("adjuster-1", FiledAtUtc.AddDays(2));

        Assert.Equal(reserveChangesBeforeClose, claim.ReserveChanges.Count);
    }

    [Fact]
    public void Close_Requires_A_Prior_Decision()
    {
        var claim = AssignedClaim();

        Assert.Throws<InvalidOperationException>(() => claim.Close("adjuster-1", FiledAtUtc.AddDays(1)));
    }

    [Fact]
    public void Close_Requires_The_Assigned_Adjuster()
    {
        var claim = AssignedClaim("adjuster-1");
        claim.Deny(ClaimDenialReason.InsufficientEvidence, "No forensic report provided.", "adjuster-1", FiledAtUtc.AddDays(1));

        Assert.Throws<InvalidOperationException>(() => claim.Close("adjuster-2", FiledAtUtc.AddDays(2)));
    }

    [Fact]
    public void A_Closed_File_Rejects_Further_Decisions()
    {
        var claim = AssignedClaim();
        claim.Accept(300_000m, "Covered loss.", null, "adjuster-1", FiledAtUtc.AddDays(1));
        claim.Close("adjuster-1", FiledAtUtc.AddDays(2));

        Assert.Throws<InvalidOperationException>(() =>
            claim.Accept(1m, "Again.", null, "adjuster-1", FiledAtUtc.AddDays(3)));
        Assert.Throws<InvalidOperationException>(() =>
            claim.Deny(ClaimDenialReason.Other, "Again.", "adjuster-1", FiledAtUtc.AddDays(3)));
        Assert.Throws<InvalidOperationException>(() => claim.Close("adjuster-1", FiledAtUtc.AddDays(3)));
    }
}
