using System.Text;
using LIAnsureProtect.Modules.Underwriting.Application.Evidence.Documents;
using LIAnsureProtect.Modules.Underwriting.Infrastructure.Evidence.Documents;

namespace LIAnsureProtect.IntegrationTests.Underwriting;

public sealed class LocalDeterministicEvidenceDocumentScannerTests
{
    [Fact]
    public async Task Clean_relevant_document_receives_advisory_plausibility_result_only()
    {
        var content = "MFA multi-factor authentication coverage report version 2026 covers privileged, email, and remote access.";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var scanner = new LocalDeterministicEvidenceDocumentScanner();

        var result = await scanner.ScanAsync(
            new EvidenceDocumentScanRequest(
                "mfa-coverage-2026.txt",
                "text/plain",
                stream.Length,
                "MultiFactorAuthentication",
                stream),
            TestContext.Current.CancellationToken);

        Assert.Equal("Plausible", result.PlausibilityStatus);
        Assert.Equal("NeedsHumanReview", result.ClaimConsistencyStatus);
        Assert.Empty(result.AdvisoryFindings);
        Assert.Equal(LocalDeterministicEvidenceDocumentScanner.AssessmentVersion, result.AssessmentVersion);
    }

    [Fact]
    public async Task Clean_but_uninformative_document_is_flagged_for_human_review_without_being_rejected()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("short note"));
        var scanner = new LocalDeterministicEvidenceDocumentScanner();

        var result = await scanner.ScanAsync(
            new EvidenceDocumentScanRequest(
                "x.txt",
                "text/plain",
                stream.Length,
                "BackupRecovery",
                stream),
            TestContext.Current.CancellationToken);

        Assert.Equal("Clean", result.ScanStatus.ToString());
        Assert.Equal("NeedsReview", result.PlausibilityStatus);
        Assert.NotEmpty(result.AdvisoryFindings);
        Assert.Equal("NeedsHumanReview", result.ClaimConsistencyStatus);
    }
}
