using LIAnsureProtect.Domain.Common;

namespace LIAnsureProtect.Domain.Quotes;

public sealed class QuoteEvidenceRequest : IHasDomainEvents
{
    private readonly List<IDomainEvent> domainEvents = [];

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
        ReviewDecision = EvidenceReviewDecisionStatus.NotReviewed;
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

    public EvidenceReviewDecisionStatus ReviewDecision { get; private set; } = EvidenceReviewDecisionStatus.NotReviewed;

    public string? ReviewReason { get; private set; }

    public string? RemediationGuidance { get; private set; }

    public string? ReviewedByUserId { get; private set; }

    public DateTime? ReviewedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();

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

        var evidenceRequest = new QuoteEvidenceRequest(
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

        evidenceRequest.domainEvents.Add(new QuoteEvidenceRequestCreatedDomainEvent(
            evidenceRequest.Id,
            evidenceRequest.QuoteId,
            evidenceRequest.SubmissionId,
            evidenceRequest.OwnerUserId,
            evidenceRequest.RequestedByUserId,
            evidenceRequest.Category,
            evidenceRequest.DueAtUtc,
            requestedAtUtc));

        return evidenceRequest;
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
        EnsureCanRespond();

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
        ResetReviewDecision();

        domainEvents.Add(new QuoteEvidenceRequestRespondedDomainEvent(
            Id,
            QuoteId,
            SubmissionId,
            OwnerUserId,
            RequestedByUserId,
            RespondedByUserId,
            Category,
            DueAtUtc,
            respondedAtUtc));
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
        ReviewDecision = EvidenceReviewDecisionStatus.Satisfied;
        ReviewReason = ReviewNotes ?? "Evidence satisfied by underwriting review.";
        RemediationGuidance = null;
        ReviewedByUserId = AcceptedByUserId;
        ReviewedAtUtc = acceptedAtUtc;

        domainEvents.Add(new QuoteEvidenceRequestAcceptedDomainEvent(
            Id,
            QuoteId,
            SubmissionId,
            OwnerUserId,
            RequestedByUserId,
            AcceptedByUserId,
            Category,
            DueAtUtc,
            acceptedAtUtc));
    }

    public void Cancel(string cancelledByUserId, string? reviewNotes, DateTime cancelledAtUtc)
    {
        EnsureOpenOrResponded();

        CancelledByUserId = ValidateRequired(cancelledByUserId, nameof(cancelledByUserId), "Cancelled-by user id is required.");
        ReviewNotes = NormalizeOptional(reviewNotes);
        CancelledAtUtc = cancelledAtUtc;
        UpdatedAtUtc = cancelledAtUtc;
        Status = EvidenceRequestStatus.Cancelled;

        domainEvents.Add(new QuoteEvidenceRequestCancelledDomainEvent(
            Id,
            QuoteId,
            SubmissionId,
            OwnerUserId,
            RequestedByUserId,
            CancelledByUserId,
            Category,
            DueAtUtc,
            cancelledAtUtc));
    }

    public void RecordFollowUpSent(string followedUpByUserId, DateTime followedUpAtUtc)
    {
        if (Status != EvidenceRequestStatus.Open)
            throw new InvalidOperationException("Only open evidence requests can receive follow-up reminders.");

        var trimmedFollowedUpByUserId = ValidateRequired(
            followedUpByUserId,
            nameof(followedUpByUserId),
            "Followed-up-by user id is required.");

        UpdatedAtUtc = followedUpAtUtc;
        domainEvents.Add(new QuoteEvidenceRequestFollowUpSentDomainEvent(
            Id,
            QuoteId,
            SubmissionId,
            OwnerUserId,
            RequestedByUserId,
            trimmedFollowedUpByUserId,
            Category,
            DueAtUtc,
            followedUpAtUtc));
    }

    public void RecordReviewDecision(
        EvidenceReviewDecisionStatus decision,
        string reason,
        string? remediationGuidance,
        string reviewedByUserId,
        DateTime reviewedAtUtc)
    {
        if (Status != EvidenceRequestStatus.Responded)
            throw new InvalidOperationException("Only responded evidence requests can receive review decisions.");

        if (decision is EvidenceReviewDecisionStatus.NotReviewed or EvidenceReviewDecisionStatus.Satisfied)
            throw new InvalidOperationException("Use a specific unfavorable review decision for this workflow.");

        ReviewReason = ValidateRequired(reason, nameof(reason), "Review reason is required.");
        ReviewedByUserId = ValidateRequired(reviewedByUserId, nameof(reviewedByUserId), "Reviewed-by user id is required.");
        RemediationGuidance = NormalizeOptional(remediationGuidance);
        if (RemediationGuidance is null)
            throw new ArgumentException("Remediation guidance is required for insufficient or clarification-needed evidence.");

        ReviewDecision = decision;
        ReviewedAtUtc = reviewedAtUtc;
        UpdatedAtUtc = reviewedAtUtc;
    }

    public void ClearDomainEvents()
    {
        domainEvents.Clear();
    }

    private void EnsureOpen()
    {
        if (Status != EvidenceRequestStatus.Open)
            throw new InvalidOperationException("Evidence request is already closed.");
    }

    private void EnsureCanRespond()
    {
        if (Status == EvidenceRequestStatus.Open)
            return;

        if (Status == EvidenceRequestStatus.Responded
            && ReviewDecision is EvidenceReviewDecisionStatus.Insufficient or EvidenceReviewDecisionStatus.NeedsClarification)
        {
            return;
        }

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

    private void ResetReviewDecision()
    {
        ReviewDecision = EvidenceReviewDecisionStatus.NotReviewed;
        ReviewReason = null;
        RemediationGuidance = null;
        ReviewedByUserId = null;
        ReviewedAtUtc = null;
    }
}
