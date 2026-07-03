using LIAnsureProtect.Modules.Claims.Domain;

namespace LIAnsureProtect.UnitTests.Modules.Claims;

public sealed class ClaimOperationsTests
{
    private static readonly DateTime FiledAtUtc = new(2026, 3, 13, 10, 0, 0, DateTimeKind.Utc);

    private static Claim FileClaim()
    {
        var claim = Claim.File(
            policyId: Guid.NewGuid(),
            submissionId: Guid.NewGuid(),
            ownerUserId: "customer-1",
            claimNumber: "CLM-2026-0A1B2C3D",
            incidentType: ClaimIncidentType.RansomwareExtortion,
            incidentAtUtc: new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc),
            discoveredAtUtc: new DateTime(2026, 3, 12, 9, 30, 0, DateTimeKind.Utc),
            description: "Ransomware encrypted the file server; extortion note received.",
            policyNumberAtFiling: "POL-2026-11111111",
            policyEffectiveAtFiling: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            policyExpirationAtFiling: new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            policyLimitAtFiling: 1_000_000m,
            policyRetentionAtFiling: 25_000m,
            filedAtUtc: FiledAtUtc);
        claim.ClearDomainEvents();

        return claim;
    }

    // --- Assignment: the M44.5 guarded claim ---

    [Fact]
    public void First_Assignment_Wins_And_Starts_The_Review()
    {
        var claim = FileClaim();

        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));

        Assert.Equal("adjuster-1", claim.AssignedAdjusterUserId);
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Contains(claim.TimelineEntries, entry => entry.EntryType == ClaimTimelineEntryType.AssignmentChanged);
        var assignedEvent = Assert.IsType<ClaimAssignedDomainEvent>(Assert.Single(claim.DomainEvents));
        Assert.Equal("adjuster-1", assignedEvent.AdjusterUserId);
    }

    [Fact]
    public void Same_Adjuster_Reassignment_Is_Idempotent()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));
        var versionAfterFirst = claim.Version;
        claim.ClearDomainEvents();

        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(2));

        Assert.Equal("adjuster-1", claim.AssignedAdjusterUserId);
        Assert.Equal(versionAfterFirst, claim.Version);
        Assert.Empty(claim.DomainEvents);
    }

    [Fact]
    public void Second_Adjuster_Is_Rejected_By_The_Domain_Guard()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => claim.AssignTo("adjuster-2", FiledAtUtc.AddHours(2)));
        Assert.Equal("adjuster-1", claim.AssignedAdjusterUserId);
    }

    [Fact]
    public void Release_Is_The_Explicit_Hand_Over()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));

        claim.ReleaseAssignment("adjuster-1", FiledAtUtc.AddHours(2));
        Assert.Null(claim.AssignedAdjusterUserId);
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);

        claim.AssignTo("adjuster-2", FiledAtUtc.AddHours(3));
        Assert.Equal("adjuster-2", claim.AssignedAdjusterUserId);
    }

    [Fact]
    public void Assignment_Of_A_Decided_Or_Closed_Claim_Is_Rejected()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));
        claim.Accept("adjuster-1", FiledAtUtc.AddHours(2));

        Assert.Throws<InvalidOperationException>(() => claim.AssignTo("adjuster-2", FiledAtUtc.AddHours(3)));

        claim.Close("adjuster-1", FiledAtUtc.AddHours(4));
        Assert.Throws<InvalidOperationException>(() => claim.ReleaseAssignment("adjuster-1", FiledAtUtc.AddHours(5)));
    }

    [Fact]
    public void Assignment_Of_An_Already_UnderReview_Claim_Does_Not_Change_Status_Twice()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));
        claim.ReleaseAssignment("adjuster-1", FiledAtUtc.AddHours(2));
        var statusChanges = claim.TimelineEntries.Count(entry => entry.EntryType == ClaimTimelineEntryType.StatusChanged);

        claim.AssignTo("adjuster-2", FiledAtUtc.AddHours(3));

        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Equal(statusChanges, claim.TimelineEntries.Count(entry => entry.EntryType == ClaimTimelineEntryType.StatusChanged));
    }

    // --- Work notes ---

    [Fact]
    public void Work_Notes_Are_Appended_With_Timeline_Entries()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));

        var note = claim.AddWorkNote("adjuster-1", "Called the insured; forensic report expected Friday.", FiledAtUtc.AddHours(2));

        Assert.Single(claim.WorkNotes);
        Assert.Equal(claim.Id, note.ClaimId);
        Assert.Equal("Called the insured; forensic report expected Friday.", note.Note);
        Assert.Contains(claim.TimelineEntries, entry => entry.EntryType == ClaimTimelineEntryType.NoteAdded);
    }

    [Fact]
    public void Empty_Work_Notes_Are_Rejected()
    {
        var claim = FileClaim();

        Assert.Throws<ArgumentException>(() => claim.AddWorkNote("adjuster-1", "  ", FiledAtUtc.AddHours(1)));
    }

    // --- Information requests ---

    [Fact]
    public void Information_Request_Flips_The_Claim_To_InformationRequested()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));
        claim.ClearDomainEvents();

        var request = claim.RequestInformation(
            "adjuster-1",
            "Proof of loss",
            "Please provide the forensic report and the extortion note.",
            FiledAtUtc.AddHours(2));

        Assert.Equal(ClaimStatus.InformationRequested, claim.Status);
        Assert.False(request.IsAnswered);
        Assert.Single(claim.InformationRequests);
        Assert.Contains(claim.TimelineEntries, entry => entry.EntryType == ClaimTimelineEntryType.InformationRequested);
        var raisedEvent = Assert.IsType<ClaimInformationRequestedDomainEvent>(Assert.Single(claim.DomainEvents));
        Assert.Equal(request.Id, raisedEvent.InformationRequestId);
    }

    [Fact]
    public void Information_Request_Requires_UnderReview()
    {
        var claim = FileClaim();

        Assert.Throws<InvalidOperationException>(() => claim.RequestInformation(
            "adjuster-1", "Proof of loss", "Please provide the forensic report.", FiledAtUtc.AddHours(1)));
    }

    [Fact]
    public void Claimant_Response_Answers_The_Request_And_Returns_To_UnderReview()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));
        var request = claim.RequestInformation(
            "adjuster-1", "Proof of loss", "Please provide the forensic report.", FiledAtUtc.AddHours(2));
        claim.ClearDomainEvents();

        claim.RespondToInformationRequest(
            request.Id, "customer-1", "Forensic report attached; ransom note photographed.", FiledAtUtc.AddHours(3));

        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.True(request.IsAnswered);
        Assert.Equal("customer-1", request.RespondedByUserId);
        Assert.Contains(claim.TimelineEntries, entry => entry.EntryType == ClaimTimelineEntryType.ClaimantResponded);
        var raisedEvent = Assert.IsType<ClaimantInformationResponseDomainEvent>(Assert.Single(claim.DomainEvents));
        Assert.Equal(request.Id, raisedEvent.InformationRequestId);
        Assert.Equal("adjuster-1", raisedEvent.AssignedAdjusterUserId);
    }

    [Fact]
    public void An_Answered_Request_Cannot_Be_Answered_Twice()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));
        var request = claim.RequestInformation(
            "adjuster-1", "Proof of loss", "Please provide the forensic report.", FiledAtUtc.AddHours(2));
        claim.RespondToInformationRequest(request.Id, "customer-1", "Attached.", FiledAtUtc.AddHours(3));

        Assert.Throws<InvalidOperationException>(() => claim.RespondToInformationRequest(
            request.Id, "customer-1", "Attached again.", FiledAtUtc.AddHours(4)));
    }

    [Fact]
    public void Responding_To_An_Unknown_Request_Is_Rejected()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => claim.RespondToInformationRequest(
            Guid.NewGuid(), "customer-1", "Attached.", FiledAtUtc.AddHours(2)));
    }

    [Fact]
    public void Empty_Information_Request_Fields_Are_Rejected()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));

        Assert.Throws<ArgumentException>(() => claim.RequestInformation(
            "adjuster-1", " ", "Message.", FiledAtUtc.AddHours(2)));
        Assert.Throws<ArgumentException>(() => claim.RequestInformation(
            "adjuster-1", "Title", " ", FiledAtUtc.AddHours(2)));
    }
}
