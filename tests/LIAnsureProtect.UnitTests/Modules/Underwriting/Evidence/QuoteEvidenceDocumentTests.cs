using LIAnsureProtect.Modules.Underwriting.Domain.Evidence.Documents;

namespace LIAnsureProtect.UnitTests.Modules.Underwriting.Evidence;

public sealed class QuoteEvidenceDocumentTests
{
    [Fact]
    public void Create_defaults_new_evidence_documents_to_pending_scan()
    {
        var document = CreateDocument();

        Assert.Equal(EvidenceDocumentScanStatus.PendingScan, document.ScanStatus);
        Assert.Null(document.ScannerProviderName);
        Assert.Null(document.ScanResultCode);
        Assert.Null(document.ScanResultReason);
        Assert.Null(document.ScannedAtUtc);
        Assert.Null(document.Sha256);
        Assert.False(document.IsDownloadAvailable);
    }

    [Fact]
    public void MarkScanClean_records_provider_result_hash_and_makes_download_available()
    {
        var document = CreateDocument();
        var scannedAtUtc = new DateTime(2026, 6, 23, 9, 0, 0, DateTimeKind.Utc);

        document.RecordScanResult(
            EvidenceDocumentScanStatus.Clean,
            "LocalDeterministicEvidenceDocumentScanner",
            "NO_THREATS_FOUND",
            "No local test threat markers were found.",
            "a4a8dfb23f95682985426a2fb3a5a6e428014a73361b0dfbbafb2b038b2bbd25",
            scannedAtUtc);

        Assert.Equal(EvidenceDocumentScanStatus.Clean, document.ScanStatus);
        Assert.Equal("LocalDeterministicEvidenceDocumentScanner", document.ScannerProviderName);
        Assert.Equal("NO_THREATS_FOUND", document.ScanResultCode);
        Assert.Equal("No local test threat markers were found.", document.ScanResultReason);
        Assert.Equal("a4a8dfb23f95682985426a2fb3a5a6e428014a73361b0dfbbafb2b038b2bbd25", document.Sha256);
        Assert.Equal(scannedAtUtc, document.ScannedAtUtc);
        Assert.True(document.IsDownloadAvailable);
    }

    [Theory]
    [InlineData(EvidenceDocumentScanStatus.Rejected)]
    [InlineData(EvidenceDocumentScanStatus.Failed)]
    public void Rejected_and_failed_documents_are_not_download_available(EvidenceDocumentScanStatus scanStatus)
    {
        var document = CreateDocument();

        document.RecordScanResult(
            scanStatus,
            "LocalDeterministicEvidenceDocumentScanner",
            scanStatus == EvidenceDocumentScanStatus.Rejected ? "THREATS_FOUND" : "SCAN_FAILED",
            "Document is not trusted for download.",
            "a4a8dfb23f95682985426a2fb3a5a6e428014a73361b0dfbbafb2b038b2bbd25",
            new DateTime(2026, 6, 23, 9, 0, 0, DateTimeKind.Utc));

        Assert.Equal(scanStatus, document.ScanStatus);
        Assert.False(document.IsDownloadAvailable);
    }

    private static QuoteEvidenceDocument CreateDocument()
    {
        return QuoteEvidenceDocument.Create(
            Guid.Parse("ca8f68fa-3c95-49f5-97c1-6df9ad0db8b3"),
            Guid.Parse("96f35d70-9c3f-446c-92a1-786ad1d63459"),
            Guid.Parse("298db473-a32e-49df-a68e-79943049d369"),
            "customer-1",
            "mfa-attestation.pdf",
            "application/pdf",
            124_000,
            "evidence-documents/document.pdf",
            "customer-1",
            new DateTime(2026, 6, 23, 8, 0, 0, DateTimeKind.Utc));
    }
}
