using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Documents;

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
