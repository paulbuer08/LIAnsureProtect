namespace LIAnsureProtect.Modules.Claims.Domain;

/// <summary>What a supporting claim document is (claimant-declared).</summary>
public enum ClaimDocumentKind
{
    ProofOfLoss,
    Invoice,
    ForensicReport,
    Correspondence,
    Other
}
