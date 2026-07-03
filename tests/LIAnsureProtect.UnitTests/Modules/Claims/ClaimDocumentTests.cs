using LIAnsureProtect.Modules.Claims.Domain;

namespace LIAnsureProtect.UnitTests.Modules.Claims;

public sealed class ClaimDocumentTests
{
    private static readonly DateTime FiledAtUtc = new(2026, 3, 13, 10, 0, 0, DateTimeKind.Utc);
    private const string ValidSha256 = "a3f5b8c9d2e1f4a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0";

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

    private static ClaimDocument AddDocument(Claim claim, string fileName = "forensic-report.pdf")
        => claim.AddDocument(
            ClaimDocumentKind.ForensicReport,
            fileName,
            "application/pdf",
            1024,
            $"claims/{claim.Id}/{Guid.NewGuid():N}",
            "customer-1",
            FiledAtUtc.AddHours(1));

    [Fact]
    public void AddDocument_Starts_In_Quarantine_With_Timeline_And_Event()
    {
        var claim = FileClaim();

        var document = AddDocument(claim);

        Assert.Equal(ClaimDocumentScanStatus.PendingScan, document.ScanStatus);
        Assert.False(document.IsDownloadAvailable);
        Assert.Equal(claim.Id, document.ClaimId);
        Assert.Equal(ClaimDocumentKind.ForensicReport, document.Kind);
        Assert.Single(claim.Documents);
        Assert.Contains(claim.TimelineEntries, entry => entry.EntryType == ClaimTimelineEntryType.DocumentUploaded);
        var uploadedEvent = Assert.IsType<ClaimDocumentUploadedDomainEvent>(Assert.Single(claim.DomainEvents));
        Assert.Equal(document.Id, uploadedEvent.DocumentId);
    }

    [Fact]
    public void AddDocument_Is_Rejected_After_A_Decision()
    {
        var claim = FileClaim();
        claim.AssignTo("adjuster-1", FiledAtUtc.AddHours(1));
        claim.Accept("adjuster-1", FiledAtUtc.AddHours(2));

        Assert.Throws<InvalidOperationException>(() => AddDocument(claim));
    }

    [Fact]
    public void Clean_Scan_Result_Makes_The_Document_Downloadable()
    {
        var claim = FileClaim();
        var document = AddDocument(claim);

        document.RecordScanResult(
            ClaimDocumentScanStatus.Clean,
            "LocalDeterministicClaimDocumentScanner",
            "NO_THREATS_FOUND",
            "No local test threat markers were found.",
            ValidSha256,
            FiledAtUtc.AddHours(2));

        Assert.True(document.IsDownloadAvailable);
        Assert.Equal(ValidSha256, document.Sha256);
    }

    [Theory]
    [InlineData(ClaimDocumentScanStatus.Rejected)]
    [InlineData(ClaimDocumentScanStatus.Failed)]
    public void Unclean_Scan_Results_Keep_The_Document_Locked(ClaimDocumentScanStatus status)
    {
        var claim = FileClaim();
        var document = AddDocument(claim);

        document.RecordScanResult(
            status,
            "LocalDeterministicClaimDocumentScanner",
            "THREATS_FOUND",
            "A test threat marker was found.",
            ValidSha256,
            FiledAtUtc.AddHours(2));

        Assert.False(document.IsDownloadAvailable);
    }

    [Fact]
    public void Scan_Result_Cannot_Stay_Pending()
    {
        var claim = FileClaim();
        var document = AddDocument(claim);

        Assert.Throws<ArgumentException>(() => document.RecordScanResult(
            ClaimDocumentScanStatus.PendingScan,
            "scanner",
            "code",
            "reason",
            ValidSha256,
            FiledAtUtc.AddHours(2)));
    }

    [Fact]
    public void Scan_Result_Requires_A_Valid_Sha256()
    {
        var claim = FileClaim();
        var document = AddDocument(claim);

        Assert.Throws<ArgumentException>(() => document.RecordScanResult(
            ClaimDocumentScanStatus.Clean,
            "scanner",
            "code",
            "reason",
            "not-a-hash",
            FiledAtUtc.AddHours(2)));
    }

    [Fact]
    public void More_Documents_Can_Be_Added_While_The_Claim_Is_Open()
    {
        var claim = FileClaim();
        var first = AddDocument(claim, "forensic-report.pdf");
        first.RecordScanResult(
            ClaimDocumentScanStatus.Rejected,
            "scanner",
            "THREATS_FOUND",
            "Marker found.",
            ValidSha256,
            FiledAtUtc.AddHours(2));

        // The replacement appends; the rejected original is never deleted.
        AddDocument(claim, "forensic-report-clean.pdf");

        Assert.Equal(2, claim.Documents.Count);
        Assert.Contains(claim.Documents, document => document.ScanStatus == ClaimDocumentScanStatus.Rejected);
    }

    [Fact]
    public void AddDocument_Validates_Required_Fields()
    {
        var claim = FileClaim();

        Assert.Throws<ArgumentException>(() => claim.AddDocument(
            ClaimDocumentKind.Invoice, " ", "application/pdf", 1024, "key", "customer-1", FiledAtUtc.AddHours(1)));
        Assert.Throws<ArgumentException>(() => claim.AddDocument(
            ClaimDocumentKind.Invoice, "invoice.pdf", "application/pdf", 0, "key", "customer-1", FiledAtUtc.AddHours(1)));
    }
}
