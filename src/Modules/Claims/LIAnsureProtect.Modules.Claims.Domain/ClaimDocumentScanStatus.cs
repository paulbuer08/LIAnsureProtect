namespace LIAnsureProtect.Modules.Claims.Domain;

/// <summary>
/// Quarantine state of a claim document. Fail-closed: only <see cref="Clean"/> documents can ever
/// be downloaded; <see cref="Rejected"/>/<see cref="Failed"/> files stay visible for audit but
/// are never trusted.
/// </summary>
public enum ClaimDocumentScanStatus
{
    PendingScan,
    Clean,
    Rejected,
    Failed
}
