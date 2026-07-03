namespace LIAnsureProtect.Modules.Underwriting.Domain.Evidence;

public sealed class QuoteEvidenceRequestReview
{
    // The only constructor: EF Core materializes through it, and the Record factory assigns
    // state via the private property setters — no parameter-heavy constructor to maintain.
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

        return new QuoteEvidenceRequestReview
        {
            Id = Guid.NewGuid(),
            EvidenceRequestId = evidenceRequest.Id,
            QuoteId = evidenceRequest.QuoteId,
            SubmissionId = evidenceRequest.SubmissionId,
            OwnerUserId = evidenceRequest.OwnerUserId,
            Category = evidenceRequest.Category,
            Decision = decision,
            Reason = reason.Trim(),
            RemediationGuidance = string.IsNullOrWhiteSpace(remediationGuidance) ? null : remediationGuidance.Trim(),
            ReviewedByUserId = reviewedByUserId.Trim(),
            ReviewedAtUtc = reviewedAtUtc,
            DocumentCount = documentCount,
            CleanDocumentCount = cleanDocumentCount
        };
    }
}
