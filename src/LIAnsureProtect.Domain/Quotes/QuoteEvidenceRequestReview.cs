namespace LIAnsureProtect.Domain.Quotes;

public sealed class QuoteEvidenceRequestReview
{
    private QuoteEvidenceRequestReview(
        Guid id,
        Guid evidenceRequestId,
        Guid quoteId,
        Guid submissionId,
        string ownerUserId,
        EvidenceRequestCategory category,
        EvidenceReviewDecisionStatus decision,
        string reason,
        string? remediationGuidance,
        string reviewedByUserId,
        DateTime reviewedAtUtc,
        int documentCount,
        int cleanDocumentCount)
    {
        Id = id;
        EvidenceRequestId = evidenceRequestId;
        QuoteId = quoteId;
        SubmissionId = submissionId;
        OwnerUserId = ownerUserId;
        Category = category;
        Decision = decision;
        Reason = reason;
        RemediationGuidance = remediationGuidance;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = reviewedAtUtc;
        DocumentCount = documentCount;
        CleanDocumentCount = cleanDocumentCount;
    }

    private QuoteEvidenceRequestReview()
    {
        OwnerUserId = string.Empty;
        Reason = string.Empty;
        ReviewedByUserId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid EvidenceRequestId { get; private set; }

    public Guid QuoteId { get; private set; }

    public Guid SubmissionId { get; private set; }

    public string OwnerUserId { get; private set; }

    public EvidenceRequestCategory Category { get; private set; }

    public EvidenceReviewDecisionStatus Decision { get; private set; }

    public string Reason { get; private set; }

    public string? RemediationGuidance { get; private set; }

    public string ReviewedByUserId { get; private set; }

    public DateTime ReviewedAtUtc { get; private set; }

    public int DocumentCount { get; private set; }

    public int CleanDocumentCount { get; private set; }

    public static QuoteEvidenceRequestReview Record(
        QuoteEvidenceRequest evidenceRequest,
        EvidenceReviewDecisionStatus decision,
        string reason,
        string? remediationGuidance,
        string reviewedByUserId,
        DateTime reviewedAtUtc,
        int documentCount,
        int cleanDocumentCount)
    {
        ArgumentNullException.ThrowIfNull(evidenceRequest);

        if (decision == EvidenceReviewDecisionStatus.NotReviewed)
            throw new ArgumentException("A review audit row must record a decision.", nameof(decision));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Review reason is required.", nameof(reason));

        if (string.IsNullOrWhiteSpace(reviewedByUserId))
            throw new ArgumentException("Reviewed-by user id is required.", nameof(reviewedByUserId));

        if (documentCount < 0)
            throw new ArgumentException("Document count cannot be negative.", nameof(documentCount));

        if (cleanDocumentCount < 0 || cleanDocumentCount > documentCount)
            throw new ArgumentException("Clean document count must be between zero and the total document count.", nameof(cleanDocumentCount));

        return new QuoteEvidenceRequestReview(
            Guid.NewGuid(),
            evidenceRequest.Id,
            evidenceRequest.QuoteId,
            evidenceRequest.SubmissionId,
            evidenceRequest.OwnerUserId,
            evidenceRequest.Category,
            decision,
            reason.Trim(),
            string.IsNullOrWhiteSpace(remediationGuidance) ? null : remediationGuidance.Trim(),
            reviewedByUserId.Trim(),
            reviewedAtUtc,
            documentCount,
            cleanDocumentCount);
    }
}
