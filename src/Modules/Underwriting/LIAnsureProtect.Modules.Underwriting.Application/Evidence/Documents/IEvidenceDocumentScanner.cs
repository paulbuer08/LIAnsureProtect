namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;

public interface IEvidenceDocumentScanner
{
    Task<EvidenceDocumentScanResult> ScanAsync(
        EvidenceDocumentScanRequest request,
        CancellationToken cancellationToken);
}

public sealed record EvidenceDocumentScanRequest(
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed record EvidenceDocumentScanResult(
    EvidenceDocumentScanStatus ScanStatus,
    string ScannerProviderName,
    string ScanResultCode,
    string ScanResultReason,
    string Sha256,
    DateTime ScannedAtUtc);

public enum EvidenceDocumentScanStatus
{
    PendingScan = 0,
    Clean = 1,
    Rejected = 2,
    Failed = 3
}
