using System.Security.Cryptography;
using System.Text;
using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence.Documents;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Evidence.Documents;

public sealed class LocalDeterministicEvidenceDocumentScanner : IEvidenceDocumentScanner
{
    public const string ProviderName = nameof(LocalDeterministicEvidenceDocumentScanner);
    private const string RejectedMarker = "MALWARE-TEST-SIGNAL";
    private const string FailedMarker = "SCAN-FAIL-TEST-SIGNAL";
    public const string AssessmentVersion = "local-advisory-evidence-v1";

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
        var advisory = AssessPlausibility(request.EvidenceCategory, text, request.OriginalFileName);

        if (text.Contains(FailedMarker, StringComparison.Ordinal))
        {
            return new EvidenceDocumentScanResult(
                EvidenceDocumentScanStatus.Failed,
                ProviderName,
                "SCAN_FAILED",
                "Local deterministic scanner simulated a provider failure.",
                sha256,
                scannedAtUtc,
                AssessmentVersion,
                "NotAssessed",
                "NotAssessed",
                []);
        }

        if (text.Contains(RejectedMarker, StringComparison.Ordinal))
        {
            return new EvidenceDocumentScanResult(
                EvidenceDocumentScanStatus.Rejected,
                ProviderName,
                "THREATS_FOUND",
                "Local deterministic scanner found a test threat marker.",
                sha256,
                scannedAtUtc,
                AssessmentVersion,
                "NotAssessed",
                "NotAssessed",
                []);
        }

        return new EvidenceDocumentScanResult(
            EvidenceDocumentScanStatus.Clean,
            ProviderName,
            "NO_THREATS_FOUND",
            "No local test threat markers were found.",
            sha256,
            scannedAtUtc,
            AssessmentVersion,
            advisory.PlausibilityStatus,
            "NeedsHumanReview",
            advisory.Findings);
    }

    private static AdvisoryAssessment AssessPlausibility(
        string category,
        string text,
        string fileName)
    {
        var findings = new List<string>();
        var normalizedText = text.ToLowerInvariant();
        if (text.Trim().Length < 40)
            findings.Add("Document contains too little readable text for a strong plausibility check.");

        var expectedTerms = category switch
        {
            "MultiFactorAuthentication" => new[] { "mfa", "multi-factor", "authentication" },
            "EndpointDetectionAndResponse" => new[] { "edr", "endpoint", "agent", "coverage" },
            "BackupRecovery" => new[] { "backup", "restore", "recovery", "immutable" },
            "IncidentResponsePlan" => new[] { "incident", "response", "roles", "escalation" },
            _ => new[] { "data", "inventory", "encryption", "security" }
        };
        if (!expectedTerms.Any(normalizedText.Contains))
            findings.Add($"Readable content does not contain expected {category} terms.");

        if (!normalizedText.Any(char.IsDigit))
            findings.Add("No readable date, version, coverage, or other numeric indicator was found.");

        if (Path.GetFileNameWithoutExtension(fileName).Length < 4)
            findings.Add("File name provides little descriptive context.");

        return new AdvisoryAssessment(
            findings.Count == 0 ? "Plausible" : "NeedsReview",
            findings);
    }

    private sealed record AdvisoryAssessment(
        string PlausibilityStatus,
        IReadOnlyCollection<string> Findings);
}
