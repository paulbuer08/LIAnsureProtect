using System.Security.Cryptography;
using System.Text;
using LIAnsureProtect.Modules.Claims.Application.Documents;
using LIAnsureProtect.Modules.Claims.Domain;

namespace LIAnsureProtect.Modules.Claims.Infrastructure.Documents;

/// <summary>
/// Local deterministic quarantine scanner (same test markers as the evidence scanner so manual
/// testing works identically across modules): a file containing MALWARE-TEST-SIGNAL is Rejected,
/// SCAN-FAIL-TEST-SIGNAL simulates a provider failure, anything else is Clean. Always records the
/// SHA-256 of the exact stored bytes.
/// </summary>
public sealed class LocalDeterministicClaimDocumentScanner : IClaimDocumentScanner
{
    public const string ProviderName = nameof(LocalDeterministicClaimDocumentScanner);
    private const string RejectedMarker = "MALWARE-TEST-SIGNAL";
    private const string FailedMarker = "SCAN-FAIL-TEST-SIGNAL";

    public async Task<ClaimDocumentScanResult> ScanAsync(
        ClaimDocumentScanRequest request,
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
            return new ClaimDocumentScanResult(
                ClaimDocumentScanStatus.Failed,
                ProviderName,
                "SCAN_FAILED",
                "Local deterministic scanner simulated a provider failure.",
                sha256,
                scannedAtUtc);
        }

        if (text.Contains(RejectedMarker, StringComparison.Ordinal))
        {
            return new ClaimDocumentScanResult(
                ClaimDocumentScanStatus.Rejected,
                ProviderName,
                "THREATS_FOUND",
                "Local deterministic scanner found a test threat marker.",
                sha256,
                scannedAtUtc);
        }

        return new ClaimDocumentScanResult(
            ClaimDocumentScanStatus.Clean,
            ProviderName,
            "NO_THREATS_FOUND",
            "No local test threat markers were found.",
            sha256,
            scannedAtUtc);
    }
}
