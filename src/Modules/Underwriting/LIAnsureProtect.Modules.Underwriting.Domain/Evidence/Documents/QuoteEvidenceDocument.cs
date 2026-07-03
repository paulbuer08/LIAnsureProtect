namespace LIAnsureProtect.Modules.Underwriting.Domain.Evidence.Documents;

public sealed class QuoteEvidenceDocument
{
    // The only constructor: EF Core materializes through it, and the Create factory assigns
    // state via the private property setters — no parameter-heavy constructor to maintain.
    private QuoteEvidenceDocument()
    {
    }

    public Guid Id { get; private set; }

    public Guid EvidenceRequestId { get; private set; }

    public Guid QuoteId { get; private set; }

    public Guid SubmissionId { get; private set; }

    public string OwnerUserId { get; private set; } = string.Empty;

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string UploadedByUserId { get; private set; } = string.Empty;

    public DateTime UploadedAtUtc { get; private set; }

    public EvidenceDocumentScanStatus ScanStatus { get; private set; } = EvidenceDocumentScanStatus.PendingScan;

    public string? ScannerProviderName { get; private set; }

    public string? ScanResultCode { get; private set; }

    public string? ScanResultReason { get; private set; }

    public DateTime? ScannedAtUtc { get; private set; }

    public string? Sha256 { get; private set; }

    public bool IsDownloadAvailable => ScanStatus == EvidenceDocumentScanStatus.Clean;

    public static QuoteEvidenceDocument Create(
        Guid evidenceRequestId,
        Guid quoteId,
        Guid submissionId,
        string ownerUserId,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        string uploadedByUserId,
        DateTime uploadedAtUtc)
    {
        ValidateGuid(evidenceRequestId, nameof(evidenceRequestId), "Evidence request id is required.");
        ValidateGuid(quoteId, nameof(quoteId), "Quote id is required.");
        ValidateGuid(submissionId, nameof(submissionId), "Submission id is required.");

        if (sizeBytes <= 0)
            throw new ArgumentException("Evidence document size must be greater than zero.", nameof(sizeBytes));

        return new QuoteEvidenceDocument
        {
            Id = Guid.NewGuid(),
            EvidenceRequestId = evidenceRequestId,
            QuoteId = quoteId,
            SubmissionId = submissionId,
            OwnerUserId = ValidateRequired(ownerUserId, nameof(ownerUserId), "Owner user id is required."),
            OriginalFileName = ValidateRequired(originalFileName, nameof(originalFileName), "Original file name is required."),
            ContentType = ValidateRequired(contentType, nameof(contentType), "Content type is required."),
            SizeBytes = sizeBytes,
            StorageKey = ValidateRequired(storageKey, nameof(storageKey), "Storage key is required."),
            UploadedByUserId = ValidateRequired(uploadedByUserId, nameof(uploadedByUserId), "Uploaded-by user id is required."),
            UploadedAtUtc = uploadedAtUtc,
            ScanStatus = EvidenceDocumentScanStatus.PendingScan
        };
    }

    public void RecordScanResult(
        EvidenceDocumentScanStatus scanStatus,
        string scannerProviderName,
        string scanResultCode,
        string scanResultReason,
        string sha256,
        DateTime scannedAtUtc)
    {
        if (scanStatus == EvidenceDocumentScanStatus.PendingScan)
            throw new ArgumentException("Scan result must be clean, rejected, or failed.", nameof(scanStatus));

        ScanStatus = scanStatus;
        ScannerProviderName = ValidateRequired(scannerProviderName, nameof(scannerProviderName), "Scanner provider name is required.");
        ScanResultCode = ValidateRequired(scanResultCode, nameof(scanResultCode), "Scan result code is required.");
        ScanResultReason = ValidateRequired(scanResultReason, nameof(scanResultReason), "Scan result reason is required.");
        Sha256 = ValidateSha256(sha256);
        ScannedAtUtc = scannedAtUtc;
    }

    private static void ValidateGuid(Guid value, string parameterName, string message)
    {
        if (value == Guid.Empty)
            throw new ArgumentException(message, parameterName);
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
