using LIAnsureProtect.Modules.Claims.Domain;

namespace LIAnsureProtect.Modules.Claims.Application.Documents;

/// <summary>
/// Quarantine scanner port for claim documents (the M28 evidence-scanner pattern, module-owned
/// because module ports never cross module boundaries). Fail-closed: the scan result decides
/// whether a document can ever be downloaded.
/// </summary>
public interface IClaimDocumentScanner
{
    Task<ClaimDocumentScanResult> ScanAsync(
        ClaimDocumentScanRequest request,
        CancellationToken cancellationToken);
}

public sealed record ClaimDocumentScanRequest(
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed record ClaimDocumentScanResult(
    ClaimDocumentScanStatus ScanStatus,
    string ScannerProviderName,
    string ScanResultCode,
    string ScanResultReason,
    string Sha256,
    DateTime ScannedAtUtc);
