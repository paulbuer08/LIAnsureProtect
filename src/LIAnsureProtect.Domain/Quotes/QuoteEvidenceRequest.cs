namespace LIAnsureProtect.Domain.Quotes;

public sealed class QuoteEvidenceRequest
{
    private QuoteEvidenceRequest(
        Guid id,
        Guid quoteId,
        Guid submissionId,
        Guid quoteReferralOperationId,
        string ownerUserId,
        string requestedByUserId,
        EvidenceRequestCategory category,
        string title,
        string description,
        DateTime dueAtUtc,
        DateTime requestedAtUtc)
    {
        Id = id;
        QuoteId = quoteId;
        SubmissionId = submissionId;
        QuoteReferralOperationId = quoteReferralOperationId;
        OwnerUserId = ownerUserId;
        RequestedByUserId = requestedByUserId;
        Category = category;
        Title = title;
        Description = description;
        DueAtUtc = dueAtUtc;
        RequestedAtUtc = requestedAtUtc;
        UpdatedAtUtc = requestedAtUtc;
        Status = EvidenceRequestStatus.Open;
    }

    private QuoteEvidenceRequest()
    {
    }

    public Guid Id { get; private set; }

    public Guid QuoteId { get; private set; }

    public Guid SubmissionId { get; private set; }

    public Guid QuoteReferralOperationId { get; private set; }

    public string OwnerUserId { get; private set; } = string.Empty;

    public EvidenceRequestCategory Category { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public DateTime DueAtUtc { get; private set; }

    public EvidenceRequestStatus Status { get; private set; }

    public string RequestedByUserId { get; private set; } = string.Empty;

    public DateTime RequestedAtUtc { get; private set; }

    public string? RespondedByUserId { get; private set; }

    public string? RespondentName { get; private set; }

    public string? RespondentTitle { get; private set; }

    public string? ResponseText { get; private set; }

    public string? AttachmentFileName { get; private set; }

    public string? AttachmentContentType { get; private set; }

    public long? AttachmentSizeBytes { get; private set; }

    public DateTime? RespondedAtUtc { get; private set; }

    public string? AcceptedByUserId { get; private set; }

    public DateTime? AcceptedAtUtc { get; private set; }

    public string? CancelledByUserId { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? ReviewNotes { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static QuoteEvidenceRequest Create(
        Guid quoteId,
        Guid submissionId,
        Guid quoteReferralOperationId,
        string ownerUserId,
        string requestedByUserId,
        EvidenceRequestCategory category,
        string title,
        string description,
        DateTime dueAtUtc,
        DateTime requestedAtUtc)
    {
        ValidateGuid(quoteId, nameof(quoteId), "Quote id is required.");
        ValidateGuid(submissionId, nameof(submissionId), "Submission id is required.");
        ValidateGuid(quoteReferralOperationId, nameof(quoteReferralOperationId), "Quote referral operation id is required.");
        var trimmedOwnerUserId = ValidateRequired(ownerUserId, nameof(ownerUserId), "Owner user id is required.");
        var trimmedRequestedByUserId = ValidateRequired(requestedByUserId, nameof(requestedByUserId), "Requested-by user id is required.");
        var trimmedTitle = ValidateRequired(title, nameof(title), "Evidence request title is required.");
        var trimmedDescription = ValidateRequired(description, nameof(description), "Evidence request description is required.");

        if (dueAtUtc < requestedAtUtc)
            throw new InvalidOperationException("Evidence request due date cannot be before request creation.");

        return new QuoteEvidenceRequest(
            Guid.NewGuid(),
            quoteId,
            submissionId,
            quoteReferralOperationId,
            trimmedOwnerUserId,
            trimmedRequestedByUserId,
            category,
            trimmedTitle,
            trimmedDescription,
            dueAtUtc,
            requestedAtUtc);
    }

    public void Respond(
        string respondedByUserId,
        string respondentName,
        string respondentTitle,
        string responseText,
        string? attachmentFileName,
        string? attachmentContentType,
        long? attachmentSizeBytes,
        DateTime respondedAtUtc)
    {
        EnsureOpen();

        RespondedByUserId = ValidateRequired(respondedByUserId, nameof(respondedByUserId), "Responded-by user id is required.");
        RespondentName = ValidateRequired(respondentName, nameof(respondentName), "Respondent name is required.");
        RespondentTitle = ValidateRequired(respondentTitle, nameof(respondentTitle), "Respondent title is required.");
        ResponseText = ValidateRequired(responseText, nameof(responseText), "Evidence response text is required.");

        if (attachmentSizeBytes is < 0)
            throw new InvalidOperationException("Attachment size cannot be negative.");

        AttachmentFileName = NormalizeOptional(attachmentFileName);
        AttachmentContentType = NormalizeOptional(attachmentContentType);
        AttachmentSizeBytes = attachmentSizeBytes;
        RespondedAtUtc = respondedAtUtc;
        UpdatedAtUtc = respondedAtUtc;
        Status = EvidenceRequestStatus.Responded;
    }

    public void Accept(string acceptedByUserId, string? reviewNotes, DateTime acceptedAtUtc)
    {
        if (Status != EvidenceRequestStatus.Responded)
            throw new InvalidOperationException("Only responded evidence requests can be accepted.");

        AcceptedByUserId = ValidateRequired(acceptedByUserId, nameof(acceptedByUserId), "Accepted-by user id is required.");
        ReviewNotes = NormalizeOptional(reviewNotes);
        AcceptedAtUtc = acceptedAtUtc;
        UpdatedAtUtc = acceptedAtUtc;
        Status = EvidenceRequestStatus.Accepted;
    }

    public void Cancel(string cancelledByUserId, string? reviewNotes, DateTime cancelledAtUtc)
    {
        EnsureOpenOrResponded();

        CancelledByUserId = ValidateRequired(cancelledByUserId, nameof(cancelledByUserId), "Cancelled-by user id is required.");
        ReviewNotes = NormalizeOptional(reviewNotes);
        CancelledAtUtc = cancelledAtUtc;
        UpdatedAtUtc = cancelledAtUtc;
        Status = EvidenceRequestStatus.Cancelled;
    }

    private void EnsureOpen()
    {
        if (Status != EvidenceRequestStatus.Open)
            throw new InvalidOperationException("Evidence request is already closed.");
    }

    private void EnsureOpenOrResponded()
    {
        if (Status is not EvidenceRequestStatus.Open and not EvidenceRequestStatus.Responded)
            throw new InvalidOperationException("Evidence request is already closed.");
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
