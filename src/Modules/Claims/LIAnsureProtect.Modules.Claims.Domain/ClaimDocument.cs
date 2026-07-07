namespace LIAnsureProtect.Modules.Claims.Domain;

/// <summary>
/// A supporting document on a claim (proof of loss, invoice, forensic report, …). Created only
/// through <see cref="Claim.AddDocument"/>; bytes live behind the platform storage port (the
/// storage key is private and never exposed to the browser). Every upload is quarantine-scanned:
/// <see cref="IsDownloadAvailable"/> is true only for a Clean scan (fail-closed).
/// </summary>
public sealed class ClaimDocument
{
    private ClaimDocument()
    {
        OriginalFileName = string.Empty;
        ContentType = string.Empty;
        StorageKey = string.Empty;
        UploadedByUserId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid ClaimId { get; private set; }

    public ClaimDocumentKind Kind { get; private set; }

    public string OriginalFileName { get; private set; }

    public string ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; }

    public string UploadedByUserId { get; private set; }

    public DateTime UploadedAtUtc { get; private set; }

    public ClaimDocumentScanStatus ScanStatus { get; private set; } = ClaimDocumentScanStatus.PendingScan;

    public string? ScannerProviderName { get; private set; }

    public string? ScanResultCode { get; private set; }

    public string? ScanResultReason { get; private set; }

    public DateTime? ScannedAtUtc { get; private set; }

    public string? Sha256 { get; private set; }

    public bool IsDownloadAvailable => ScanStatus == ClaimDocumentScanStatus.Clean;

    internal static ClaimDocument Create(
        Guid claimId,
        ClaimDocumentKind kind,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        string uploadedByUserId,
        DateTime uploadedAtUtc)
    {
        if (claimId == Guid.Empty)
            throw new ArgumentException("Claim id is required.", nameof(claimId));

        if (sizeBytes <= 0)
            throw new ArgumentException("Claim document size must be greater than zero.", nameof(sizeBytes));

        return new ClaimDocument
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            Kind = kind,
            OriginalFileName = ValidateRequired(originalFileName, nameof(originalFileName), "Original file name is required."),
            ContentType = ValidateRequired(contentType, nameof(contentType), "Content type is required."),
            SizeBytes = sizeBytes,
            StorageKey = ValidateRequired(storageKey, nameof(storageKey), "Storage key is required."),
            UploadedByUserId = ValidateRequired(uploadedByUserId, nameof(uploadedByUserId), "Uploaded-by user id is required."),
            UploadedAtUtc = uploadedAtUtc,
            ScanStatus = ClaimDocumentScanStatus.PendingScan
        };
    }

    public void RecordScanResult(
        ClaimDocumentScanStatus scanStatus,
        string scannerProviderName,
        string scanResultCode,
        string scanResultReason,
        string sha256,
        DateTime scannedAtUtc)
    {
        if (scanStatus == ClaimDocumentScanStatus.PendingScan)
            throw new ArgumentException("Scan result must be clean, rejected, or failed.", nameof(scanStatus));

        ScanStatus = scanStatus;
        ScannerProviderName = ValidateRequired(scannerProviderName, nameof(scannerProviderName), "Scanner provider name is required.");
        ScanResultCode = ValidateRequired(scanResultCode, nameof(scanResultCode), "Scan result code is required.");
        ScanResultReason = ValidateRequired(scanResultReason, nameof(scanResultReason), "Scan result reason is required.");
        Sha256 = ValidateSha256(sha256);
        ScannedAtUtc = scannedAtUtc;
    }

    private static string ValidateRequired(string value, string parameterName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(message, parameterName);

        return value.Trim();
    }

    private static string ValidateSha256(string value)
    {
        var trimmed = ValidateRequired(value, nameof(value), "SHA-256 hash is required.");
        if (trimmed.Length != 64 || trimmed.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("SHA-256 hash must be a 64-character hexadecimal value.", nameof(value));

        return trimmed.ToLowerInvariant();
    }
}
