namespace LIAnsureProtect.Domain.Quotes;

public sealed class QuoteEvidenceDocument
{
    private QuoteEvidenceDocument(
        Guid id,
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
        Id = id;
        EvidenceRequestId = evidenceRequestId;
        QuoteId = quoteId;
        SubmissionId = submissionId;
        OwnerUserId = ownerUserId;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        UploadedByUserId = uploadedByUserId;
        UploadedAtUtc = uploadedAtUtc;
    }

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

        return new QuoteEvidenceDocument(
            Guid.NewGuid(),
            evidenceRequestId,
            quoteId,
            submissionId,
            ValidateRequired(ownerUserId, nameof(ownerUserId), "Owner user id is required."),
            ValidateRequired(originalFileName, nameof(originalFileName), "Original file name is required."),
            ValidateRequired(contentType, nameof(contentType), "Content type is required."),
            sizeBytes,
            ValidateRequired(storageKey, nameof(storageKey), "Storage key is required."),
            ValidateRequired(uploadedByUserId, nameof(uploadedByUserId), "Uploaded-by user id is required."),
            uploadedAtUtc);
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
}
