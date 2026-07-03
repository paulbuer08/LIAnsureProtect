using LIAnsureProtect.Modules.Claims.Domain;

namespace LIAnsureProtect.UnitTests.Modules.Claims;

public sealed class ClaimTests
{
    private static readonly DateTime PolicyEffectiveAtUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PolicyExpirationAtUtc = new(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime IncidentAtUtc = new(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DiscoveredAtUtc = new(2026, 3, 12, 9, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime FiledAtUtc = new(2026, 3, 13, 10, 0, 0, DateTimeKind.Utc);

    private static Claim FileValidClaim()
    {
        return Claim.File(
            policyId: Guid.NewGuid(),
            submissionId: Guid.NewGuid(),
            ownerUserId: "customer-1",
            claimNumber: "CLM-2026-0A1B2C3D",
            incidentType: ClaimIncidentType.RansomwareExtortion,
            incidentAtUtc: IncidentAtUtc,
            discoveredAtUtc: DiscoveredAtUtc,
            description: "Ransomware encrypted the file server; extortion note received.",
            policyNumberAtFiling: "POL-2026-11111111",
            policyEffectiveAtFiling: PolicyEffectiveAtUtc,
            policyExpirationAtFiling: PolicyExpirationAtUtc,
            policyLimitAtFiling: 1_000_000m,
            policyRetentionAtFiling: 25_000m,
            filedAtUtc: FiledAtUtc);
    }

    [Fact]
    public void File_Creates_Filed_Claim_With_Policy_Snapshot_And_Timeline()
    {
        var claim = FileValidClaim();

        Assert.NotEqual(Guid.Empty, claim.Id);
        Assert.Equal(ClaimStatus.Filed, claim.Status);
        Assert.Equal("CLM-2026-0A1B2C3D", claim.ClaimNumber);
        Assert.Equal("customer-1", claim.OwnerUserId);
        Assert.Equal("POL-2026-11111111", claim.PolicyNumberAtFiling);
        Assert.Equal(1_000_000m, claim.PolicyLimitAtFiling);
        Assert.Equal(25_000m, claim.PolicyRetentionAtFiling);
        Assert.Equal(FiledAtUtc, claim.FiledAtUtc);
        Assert.Equal(FiledAtUtc, claim.UpdatedAtUtc);
        var entry = Assert.Single(claim.TimelineEntries);
        Assert.Equal(ClaimTimelineEntryType.ClaimFiled, entry.EntryType);
        Assert.Equal("customer-1", entry.CreatedByUserId);
    }

    [Fact]
    public void File_Raises_ClaimFiledDomainEvent()
    {
        var claim = FileValidClaim();

        var domainEvent = Assert.Single(claim.DomainEvents);
        var filedEvent = Assert.IsType<ClaimFiledDomainEvent>(domainEvent);
        Assert.Equal(claim.Id, filedEvent.ClaimId);
        Assert.Equal(claim.ClaimNumber, filedEvent.ClaimNumber);
        Assert.Equal(claim.PolicyId, filedEvent.PolicyId);
        Assert.Equal(claim.OwnerUserId, filedEvent.OwnerUserId);
        Assert.Equal(FiledAtUtc, filedEvent.OccurredAtUtc);
    }

    [Fact]
    public void File_Trims_Text_Fields()
    {
        var claim = Claim.File(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  customer-1  ",
            "  CLM-2026-0A1B2C3D  ",
            ClaimIncidentType.Other,
            IncidentAtUtc,
            DiscoveredAtUtc,
            "  Something happened.  ",
            "  POL-2026-11111111  ",
            PolicyEffectiveAtUtc,
            PolicyExpirationAtUtc,
            1_000_000m,
            25_000m,
            FiledAtUtc);

        Assert.Equal("customer-1", claim.OwnerUserId);
        Assert.Equal("CLM-2026-0A1B2C3D", claim.ClaimNumber);
        Assert.Equal("Something happened.", claim.Description);
        Assert.Equal("POL-2026-11111111", claim.PolicyNumberAtFiling);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void File_Rejects_Missing_Description(string description)
    {
        Assert.Throws<ArgumentException>(() => Claim.File(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer-1",
            "CLM-2026-0A1B2C3D",
            ClaimIncidentType.Other,
            IncidentAtUtc,
            DiscoveredAtUtc,
            description,
            "POL-2026-11111111",
            PolicyEffectiveAtUtc,
            PolicyExpirationAtUtc,
            1_000_000m,
            25_000m,
            FiledAtUtc));
    }

    [Fact]
    public void File_Rejects_Empty_PolicyId()
    {
        Assert.Throws<ArgumentException>(() => Claim.File(
            Guid.Empty,
            Guid.NewGuid(),
            "customer-1",
            "CLM-2026-0A1B2C3D",
            ClaimIncidentType.Other,
            IncidentAtUtc,
            DiscoveredAtUtc,
            "Something happened.",
            "POL-2026-11111111",
            PolicyEffectiveAtUtc,
            PolicyExpirationAtUtc,
            1_000_000m,
            25_000m,
            FiledAtUtc));
    }

    [Fact]
    public void File_Rejects_Discovery_Before_Incident()
    {
        Assert.Throws<InvalidOperationException>(() => Claim.File(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer-1",
            "CLM-2026-0A1B2C3D",
            ClaimIncidentType.Other,
            IncidentAtUtc,
            IncidentAtUtc.AddDays(-1),
            "Something happened.",
            "POL-2026-11111111",
            PolicyEffectiveAtUtc,
            PolicyExpirationAtUtc,
            1_000_000m,
            25_000m,
            FiledAtUtc));
    }

    [Fact]
    public void File_Rejects_Incident_Outside_Policy_Period()
    {
        Assert.Throws<InvalidOperationException>(() => Claim.File(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer-1",
            "CLM-2026-0A1B2C3D",
            ClaimIncidentType.Other,
            PolicyEffectiveAtUtc.AddDays(-1),
            DiscoveredAtUtc,
            "Something happened.",
            "POL-2026-11111111",
            PolicyEffectiveAtUtc,
            PolicyExpirationAtUtc,
            1_000_000m,
            25_000m,
            FiledAtUtc));
    }

    [Fact]
    public void Legal_Lifecycle_Walk_Succeeds_And_Appends_Timeline()
    {
        var claim = FileValidClaim();
        var step = FiledAtUtc;

        claim.StartReview("adjuster-1", step = step.AddHours(1));
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);

        claim.RequestInformation("adjuster-1", step = step.AddHours(1));
        Assert.Equal(ClaimStatus.InformationRequested, claim.Status);

        claim.RecordClaimantResponse("customer-1", step = step.AddHours(1));
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);

        claim.Accept("adjuster-1", step = step.AddHours(1));
        Assert.Equal(ClaimStatus.Accepted, claim.Status);

        claim.Close("adjuster-1", step = step.AddHours(1));
        Assert.Equal(ClaimStatus.Closed, claim.Status);
        Assert.Equal(step, claim.UpdatedAtUtc);

        // filed + five transitions
        Assert.Equal(6, claim.TimelineEntries.Count);
        Assert.All(claim.TimelineEntries, entry => Assert.Equal(claim.Id, entry.ClaimId));
    }

    [Fact]
    public void Deny_Then_Close_Is_Legal()
    {
        var claim = FileValidClaim();
        claim.StartReview("adjuster-1", FiledAtUtc.AddHours(1));

        claim.Deny("adjuster-1", FiledAtUtc.AddHours(2));
        Assert.Equal(ClaimStatus.Denied, claim.Status);

        claim.Close("adjuster-1", FiledAtUtc.AddHours(3));
        Assert.Equal(ClaimStatus.Closed, claim.Status);
    }

    [Fact]
    public void Accept_Requires_UnderReview()
    {
        var claim = FileValidClaim();

        Assert.Throws<InvalidOperationException>(() => claim.Accept("adjuster-1", FiledAtUtc.AddHours(1)));
    }

    [Fact]
    public void Deny_Requires_UnderReview()
    {
        var claim = FileValidClaim();

        Assert.Throws<InvalidOperationException>(() => claim.Deny("adjuster-1", FiledAtUtc.AddHours(1)));
    }

    [Fact]
    public void StartReview_Rejects_NonFiled_Status()
    {
        var claim = FileValidClaim();
        claim.StartReview("adjuster-1", FiledAtUtc.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => claim.StartReview("adjuster-1", FiledAtUtc.AddHours(2)));
    }

    [Fact]
    public void RequestInformation_Rejects_Filed_Status()
    {
        var claim = FileValidClaim();

        Assert.Throws<InvalidOperationException>(() => claim.RequestInformation("adjuster-1", FiledAtUtc.AddHours(1)));
    }

    [Fact]
    public void Close_Requires_Decision()
    {
        var claim = FileValidClaim();
        claim.StartReview("adjuster-1", FiledAtUtc.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => claim.Close("adjuster-1", FiledAtUtc.AddHours(2)));
    }

    [Fact]
    public void Closed_Claim_Rejects_All_Transitions()
    {
        var claim = FileValidClaim();
        claim.StartReview("adjuster-1", FiledAtUtc.AddHours(1));
        claim.Accept("adjuster-1", FiledAtUtc.AddHours(2));
        claim.Close("adjuster-1", FiledAtUtc.AddHours(3));

        Assert.Throws<InvalidOperationException>(() => claim.StartReview("adjuster-1", FiledAtUtc.AddHours(4)));
        Assert.Throws<InvalidOperationException>(() => claim.RequestInformation("adjuster-1", FiledAtUtc.AddHours(4)));
        Assert.Throws<InvalidOperationException>(() => claim.RecordClaimantResponse("customer-1", FiledAtUtc.AddHours(4)));
        Assert.Throws<InvalidOperationException>(() => claim.Accept("adjuster-1", FiledAtUtc.AddHours(4)));
        Assert.Throws<InvalidOperationException>(() => claim.Deny("adjuster-1", FiledAtUtc.AddHours(4)));
        Assert.Throws<InvalidOperationException>(() => claim.Close("adjuster-1", FiledAtUtc.AddHours(4)));
    }

    [Fact]
    public void Transitions_Require_A_User_Id()
    {
        var claim = FileValidClaim();

        Assert.Throws<ArgumentException>(() => claim.StartReview("  ", FiledAtUtc.AddHours(1)));
    }

    [Fact]
    public void Every_Mutation_Bumps_The_Concurrency_Version()
    {
        var claim = FileValidClaim();
        var versionAfterFiling = claim.Version;

        claim.StartReview("adjuster-1", FiledAtUtc.AddHours(1));
        Assert.Equal(versionAfterFiling + 1, claim.Version);

        claim.RequestInformation("adjuster-1", FiledAtUtc.AddHours(2));
        Assert.Equal(versionAfterFiling + 2, claim.Version);
    }
}
