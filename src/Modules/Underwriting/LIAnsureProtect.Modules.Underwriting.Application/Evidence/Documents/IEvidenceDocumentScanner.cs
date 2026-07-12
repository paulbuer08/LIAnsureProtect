using LIAnsureProtect.Modules.Underwriting.Domain.Evidence.Documents;

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
    string EvidenceCategory,
    Stream Content);

public sealed record EvidenceDocumentScanResult(
    EvidenceDocumentScanStatus ScanStatus,
    string ScannerProviderName,
    string ScanResultCode,
    string ScanResultReason,
    string Sha256,
    DateTime ScannedAtUtc,
    string AssessmentVersion,
    string PlausibilityStatus,
    string ClaimConsistencyStatus,
    IReadOnlyCollection<string> AdvisoryFindings);
