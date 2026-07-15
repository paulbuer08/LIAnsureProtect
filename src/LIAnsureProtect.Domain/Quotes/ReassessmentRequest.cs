using LIAnsureProtect.Platform.Abstractions.DomainEvents;

namespace LIAnsureProtect.Domain.Quotes;

public sealed class ReassessmentRequest : IHasDomainEvents
{
    private readonly List<IDomainEvent> domainEvents = [];

    private ReassessmentRequest()
    {
    }

    public Guid Id { get; private set; }
    public Guid SubmissionId { get; private set; }
    public Guid BaseQuoteId { get; private set; }
    public int BaseQuoteVersion { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public string RequestPayloadJson { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public ReassessmentRequestStatus Status { get; private set; }
    public string RequestedByUserId { get; private set; } = string.Empty;
    public DateTime RequestedAtUtc { get; private set; }
    public string? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? DecisionReason { get; private set; }
    public Guid? CreatedQuoteId { get; private set; }
    public string SubmissionReference { get; private set; } = string.Empty;
    public string CompanyName { get; private set; } = string.Empty;
    public int Version { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();

    public static ReassessmentRequest Create(
        Guid submissionId,
        Guid baseQuoteId,
        int baseQuoteVersion,
        string ownerUserId,
        string requestPayloadJson,
        string requestFingerprint,
        string requestedByUserId,
        DateTime requestedAtUtc,
        string? submissionReference = null,
        string? companyName = null)
    {
        if (submissionId == Guid.Empty)
            throw new ArgumentException("Submission id is required.", nameof(submissionId));
        if (baseQuoteId == Guid.Empty)
            throw new ArgumentException("Base quote id is required.", nameof(baseQuoteId));
        ArgumentOutOfRangeException.ThrowIfLessThan(baseQuoteVersion, 1);
        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("Owner user id is required.", nameof(ownerUserId));
        if (string.IsNullOrWhiteSpace(requestPayloadJson))
            throw new ArgumentException("Request payload is required.", nameof(requestPayloadJson));
        if (string.IsNullOrWhiteSpace(requestFingerprint))
            throw new ArgumentException("Request fingerprint is required.", nameof(requestFingerprint));
        if (string.IsNullOrWhiteSpace(requestedByUserId))
            throw new ArgumentException("Requested-by user id is required.", nameof(requestedByUserId));

        var request = new ReassessmentRequest
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            BaseQuoteId = baseQuoteId,
            BaseQuoteVersion = baseQuoteVersion,
            OwnerUserId = ownerUserId.Trim(),
            RequestPayloadJson = requestPayloadJson,
            RequestFingerprint = requestFingerprint.Trim(),
            Status = ReassessmentRequestStatus.Pending,
            RequestedByUserId = requestedByUserId.Trim(),
            RequestedAtUtc = requestedAtUtc,
            SubmissionReference = string.IsNullOrWhiteSpace(submissionReference)
                ? $"SUB-LEGACY-{submissionId:N}"[..30]
                : submissionReference.Trim(),
            CompanyName = string.IsNullOrWhiteSpace(companyName) ? "Company not provided" : companyName.Trim()
        };

        request.domainEvents.Add(new ReassessmentReviewRequestedDomainEvent(
            request.Id,
            request.SubmissionId,
            request.BaseQuoteId,
            request.BaseQuoteVersion,
            request.OwnerUserId,
            requestedAtUtc,
            request.SubmissionReference,
            request.CompanyName));

        return request;
    }

    public void Approve(Guid createdQuoteId, string reviewedByUserId, string reason, DateTime reviewedAtUtc)
    {
        if (createdQuoteId == Guid.Empty)
            throw new ArgumentException("Created quote id is required.", nameof(createdQuoteId));

        Complete(ReassessmentRequestStatus.Approved, reviewedByUserId, reason, reviewedAtUtc, createdQuoteId);
    }

    public void Decline(string reviewedByUserId, string reason, DateTime reviewedAtUtc)
    {
        Complete(ReassessmentRequestStatus.Declined, reviewedByUserId, reason, reviewedAtUtc, null);
    }

    public void MarkStale(string reviewedByUserId, string reason, DateTime reviewedAtUtc)
    {
        Complete(ReassessmentRequestStatus.Stale, reviewedByUserId, reason, reviewedAtUtc, null);
    }

    public void ClearDomainEvents() => domainEvents.Clear();

    private void Complete(
        ReassessmentRequestStatus status,
        string reviewedByUserId,
        string reason,
        DateTime reviewedAtUtc,
        Guid? createdQuoteId)
    {
        if (Status != ReassessmentRequestStatus.Pending)
            throw new InvalidOperationException("Only a pending reassessment request can be reviewed.");
        if (string.IsNullOrWhiteSpace(reviewedByUserId))
            throw new ArgumentException("Reviewed-by user id is required.", nameof(reviewedByUserId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A review reason is required.", nameof(reason));

        Status = status;
        ReviewedByUserId = reviewedByUserId.Trim();
        ReviewedAtUtc = reviewedAtUtc;
        DecisionReason = reason.Trim();
        CreatedQuoteId = createdQuoteId;
        Version++;

        domainEvents.Add(new ReassessmentReviewDecisionRecordedDomainEvent(
            Id,
            SubmissionId,
            BaseQuoteId,
            BaseQuoteVersion,
            CreatedQuoteId,
            OwnerUserId,
            Status.ToString(),
            DecisionReason,
            reviewedAtUtc,
            SubmissionReference,
            CompanyName));
    }
}
