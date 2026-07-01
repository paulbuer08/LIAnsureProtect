using System.Security.Cryptography;
using System.Text;
using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Evidence.Documents;

public sealed class LocalDeterministicEvidenceDocumentScanner : IEvidenceDocumentScanner
{
    public const string ProviderName = nameof(LocalDeterministicEvidenceDocumentScanner);
    private const string RejectedMarker = "MALWARE-TEST-SIGNAL";
    private const string FailedMarker = "SCAN-FAIL-TEST-SIGNAL";

    public async Task<EvidenceDocumentScanResult> ScanAsync(
        EvidenceDocumentScanRequest request,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await request.Content.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var text = Encoding.UTF8.GetString(bytes);
        var scannedAtUtc = DateTime.UtcNow;

        if (text.Contains(FailedMarker, StringComparison.Ordinal))
        {
            return new EvidenceDocumentScanResult(
                EvidenceDocumentScanStatus.Failed,
                ProviderName,
                "SCAN_FAILED",
                "Local deterministic scanner simulated a provider failure.",
                sha256,
                scannedAtUtc);
        }

        if (text.Contains(RejectedMarker, StringComparison.Ordinal))
        {
            return new EvidenceDocumentScanResult(
                EvidenceDocumentScanStatus.Rejected,
                ProviderName,
                "THREATS_FOUND",
                "Local deterministic scanner found a test threat marker.",
                sha256,
                scannedAtUtc);
        }

        return new EvidenceDocumentScanResult(
            EvidenceDocumentScanStatus.Clean,
            ProviderName,
            "NO_THREATS_FOUND",
            "No local test threat markers were found.",
            sha256,
            scannedAtUtc);
    }
}
