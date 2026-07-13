using LIAnsureProtect.Platform.Abstractions.DomainEvents;

namespace LIAnsureProtect.Domain.Quotes;

public sealed class Quote : IHasDomainEvents
{
    private readonly List<IDomainEvent> domainEvents = [];
    private readonly List<ControlAssertion> controlAssertions = [];

    // The only constructor: EF Core materializes through it, and the Generate factory assigns
    // state via the private property setters. Keeping construction property-based (instead of a
    // 20+ parameter constructor) keeps the aggregate honest as decision/acceptance fields grow.
    private Quote()
    {
        OwnerUserId = string.Empty;
        StrategyName = string.Empty;
        Subjectivities = string.Empty;
        ReferralReasons = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid SubmissionId { get; private set; }

    public string OwnerUserId { get; private set; }

    public decimal Premium { get; private set; }

    public decimal RequestedLimit { get; private set; }

    public decimal Retention { get; private set; }

    public CyberRiskTier RiskTier { get; private set; }

    public QuoteStatus Status { get; private set; }

    public int Version { get; private set; }

    public Guid? SupersedesQuoteId { get; private set; }

    public QuoteAssuranceStatus AssuranceStatus { get; private set; }

    public int EvidenceRequiredCount { get; private set; }

    public int EvidenceSatisfiedCount { get; private set; }

    public string? AttestedByUserId { get; private set; }

    public string? AttestedByName { get; private set; }

    public string? AttestedByTitle { get; private set; }

    public string? AttestationWordingVersion { get; private set; }

    public DateTime? AttestedAtUtc { get; private set; }

    public string StrategyName { get; private set; }

    public string Subjectivities { get; private set; }

    public string ReferralReasons { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public string? ReviewedByUserId { get; private set; }

    public DateTime? ReviewedAtUtc { get; private set; }

    public string? UnderwritingDecisionReason { get; private set; }

    public string? UnderwritingDecisionNotes { get; private set; }

    public string? AcceptedByUserId { get; private set; }

    public string? AcceptedByName { get; private set; }

    public string? AcceptedByTitle { get; private set; }

    public bool SubjectivitiesAcknowledged { get; private set; }

    public DateTime? AcceptedAtUtc { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();

    public IReadOnlyCollection<ControlAssertion> ControlAssertions => controlAssertions.AsReadOnly();

    public static Quote Generate(
        Guid submissionId,
        string ownerUserId,
        decimal premium,
        decimal requestedLimit,
        decimal retention,
        CyberRiskTier riskTier,
        string strategyName,
        IReadOnlyCollection<string> subjectivities,
        IReadOnlyCollection<string> referralReasons,
        DateTime createdAtUtc,
        int version = 1,
        Guid? supersedesQuoteId = null,
        string? attestedByUserId = null,
        string? attestedByName = null,
        string? attestedByTitle = null,
        string? attestationWordingVersion = null,
        int evidenceRequiredCount = 0)
    {
        if (submissionId == Guid.Empty)
            throw new ArgumentException("Submission id is required.", nameof(submissionId));

        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("Owner user id is required.", nameof(ownerUserId));

        if (premium <= 0)
            throw new ArgumentOutOfRangeException(nameof(premium), "Premium must be greater than zero.");

        if (requestedLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedLimit), "Requested limit must be greater than zero.");

        if (retention <= 0)
            throw new ArgumentOutOfRangeException(nameof(retention), "Retention must be greater than zero.");

        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(evidenceRequiredCount);

        var status = referralReasons.Count == 0
            ? QuoteStatus.Quoted
            : QuoteStatus.Referred;

        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            OwnerUserId = ownerUserId,
            Premium = premium,
            RequestedLimit = requestedLimit,
            Retention = retention,
            RiskTier = riskTier,
            Status = status,
            Version = version,
            SupersedesQuoteId = supersedesQuoteId,
            AssuranceStatus = evidenceRequiredCount == 0
                ? QuoteAssuranceStatus.SelfAttested
                : QuoteAssuranceStatus.EvidenceRequired,
            EvidenceRequiredCount = evidenceRequiredCount,
            AttestedByUserId = NormalizeOptional(attestedByUserId),
            AttestedByName = NormalizeOptional(attestedByName),
            AttestedByTitle = NormalizeOptional(attestedByTitle),
            AttestationWordingVersion = NormalizeOptional(attestationWordingVersion),
            AttestedAtUtc = string.IsNullOrWhiteSpace(attestedByUserId) ? null : createdAtUtc,
            StrategyName = strategyName,
            Subjectivities = JoinLines(subjectivities),
            ReferralReasons = JoinLines(referralReasons),
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = createdAtUtc.AddDays(30)
        };

        quote.domainEvents.Add(new QuoteGeneratedDomainEvent(
            quote.Id,
            quote.SubmissionId,
            quote.OwnerUserId,
            quote.Status,
            createdAtUtc,
            quote.Version,
            quote.Premium,
            quote.ExpiresAtUtc));

        return quote;
    }

    public void AddControlAssertion(ControlAssertion assertion)
    {
        ArgumentNullException.ThrowIfNull(assertion);

        if (assertion.QuoteId != Id || assertion.QuoteVersion != Version)
            throw new InvalidOperationException("Control assertion must belong to this quote version.");

        if (controlAssertions.Any(existing => existing.ControlType == assertion.ControlType))
            throw new InvalidOperationException("A quote version can contain only one assertion per control type.");

        controlAssertions.Add(assertion);
    }

    public void RecordAssuranceDecision(
        ControlType controlType,
        bool satisfied,
        string reviewedByUserId,
        DateTime reviewedAtUtc)
    {
        var assertion = controlAssertions.SingleOrDefault(item => item.ControlType == controlType);
        if (assertion is null || !assertion.EvidenceRequired)
            return;

        assertion.RecordHumanVerification(reviewedByUserId, satisfied, reviewedAtUtc);
        EvidenceSatisfiedCount = controlAssertions.Count(item =>
            item.EvidenceRequired && item.AssuranceState == ControlAssuranceState.HumanVerified);

        AssuranceStatus = controlAssertions.Any(item =>
            item.EvidenceRequired && item.AssuranceState == ControlAssuranceState.Rejected)
                ? QuoteAssuranceStatus.Rejected
                : EvidenceSatisfiedCount == EvidenceRequiredCount
                    ? QuoteAssuranceStatus.Verified
                    : QuoteAssuranceStatus.EvidenceRequired;
    }

    public void Supersede(DateTime supersededAtUtc)
    {
        if (Status is QuoteStatus.Accepted or QuoteStatus.Bound)
            throw new InvalidOperationException("Accepted or bound quotes cannot be reassessed. Use endorsement or renewal workflows after binding.");

        if (Status == QuoteStatus.Superseded)
            throw new InvalidOperationException("This quote version is already superseded.");

        if (supersededAtUtc < CreatedAtUtc)
            throw new InvalidOperationException("A quote cannot be superseded before it was created.");

        Status = QuoteStatus.Superseded;
    }

    public QuoteUnderwritingReview ApproveReferral(
        string reviewedByUserId,
        string reason,
        string? notes,
        DateTime reviewedAtUtc)
    {
        EnsureCanReview();
        ValidateReviewInputs(reviewedByUserId, reason);

        var premiumBefore = Premium;
        var retentionBefore = Retention;

        Status = QuoteStatus.Approved;
        RecordDecisionSnapshot(reviewedByUserId, reason, notes, reviewedAtUtc);

        var review = QuoteUnderwritingReview.Record(
            Id,
            QuoteUnderwritingDecision.Approved,
            reviewedByUserId,
            reason,
            notes,
            premiumBefore,
            Premium,
            retentionBefore,
            Retention,
            reviewedAtUtc);

        RecordUnderwritingDecisionEvent(review.Decision, reviewedAtUtc);

        return review;
    }

    public QuoteUnderwritingReview DeclineReferral(
        string reviewedByUserId,
        string reason,
        string? notes,
        DateTime reviewedAtUtc)
    {
        EnsureCanReview();
        ValidateReviewInputs(reviewedByUserId, reason);

        var premiumBefore = Premium;
        var retentionBefore = Retention;

        Status = QuoteStatus.Declined;
        RecordDecisionSnapshot(reviewedByUserId, reason, notes, reviewedAtUtc);

        var review = QuoteUnderwritingReview.Record(
            Id,
            QuoteUnderwritingDecision.Declined,
            reviewedByUserId,
            reason,
            notes,
            premiumBefore,
            Premium,
            retentionBefore,
            Retention,
            reviewedAtUtc);

        RecordUnderwritingDecisionEvent(review.Decision, reviewedAtUtc);

        return review;
    }

    public QuoteUnderwritingReview AdjustReferral(
        string reviewedByUserId,
        decimal adjustedPremium,
        decimal adjustedRetention,
        string? updatedSubjectivities,
        string reason,
        string? notes,
        DateTime reviewedAtUtc)
    {
        EnsureCanReview();
        ValidateReviewInputs(reviewedByUserId, reason);

        if (adjustedPremium <= 0)
            throw new ArgumentOutOfRangeException(nameof(adjustedPremium), "Adjusted premium must be greater than zero.");

        if (adjustedRetention <= 0)
            throw new ArgumentOutOfRangeException(nameof(adjustedRetention), "Adjusted retention must be greater than zero.");

        var premiumBefore = Premium;
        var retentionBefore = Retention;

        Premium = adjustedPremium;
        Retention = adjustedRetention;
        if (updatedSubjectivities is not null)
            Subjectivities = updatedSubjectivities.Trim();

        Status = QuoteStatus.Approved;
        RecordDecisionSnapshot(reviewedByUserId, reason, notes, reviewedAtUtc);

        var review = QuoteUnderwritingReview.Record(
            Id,
            QuoteUnderwritingDecision.Adjusted,
            reviewedByUserId,
            reason,
            notes,
            premiumBefore,
            Premium,
            retentionBefore,
            Retention,
            reviewedAtUtc);

        RecordUnderwritingDecisionEvent(review.Decision, reviewedAtUtc);

        return review;
    }

    public void Accept(
        string acceptedByUserId,
        string acceptedByName,
        string acceptedByTitle,
        bool subjectivitiesAcknowledged,
        DateTime acceptedAtUtc)
    {
        if (AssuranceStatus is QuoteAssuranceStatus.EvidenceRequired or QuoteAssuranceStatus.Rejected)
            throw new InvalidOperationException("Required control evidence must be satisfied before this provisional quote can be accepted.");

        if (Status != QuoteStatus.Quoted && Status != QuoteStatus.Approved)
            throw new InvalidOperationException("Only quoted or approved quotes can be accepted.");

        if (acceptedAtUtc > ExpiresAtUtc)
            throw new InvalidOperationException("Expired quotes cannot be accepted.");

        if (string.IsNullOrWhiteSpace(acceptedByUserId))
            throw new ArgumentException("Accepted by user id is required.", nameof(acceptedByUserId));

        if (string.IsNullOrWhiteSpace(acceptedByName))
            throw new ArgumentException("Accepted by name is required.", nameof(acceptedByName));

        if (string.IsNullOrWhiteSpace(acceptedByTitle))
            throw new ArgumentException("Accepted by title is required.", nameof(acceptedByTitle));

        if (!subjectivitiesAcknowledged)
            throw new InvalidOperationException("Quote subjectivities must be acknowledged before acceptance.");

        Status = QuoteStatus.Accepted;
        AcceptedByUserId = acceptedByUserId.Trim();
        AcceptedByName = acceptedByName.Trim();
        AcceptedByTitle = acceptedByTitle.Trim();
        SubjectivitiesAcknowledged = subjectivitiesAcknowledged;
        AcceptedAtUtc = acceptedAtUtc;

        domainEvents.Add(new QuoteAcceptedDomainEvent(
            Id,
            SubmissionId,
            OwnerUserId,
            AcceptedByUserId,
            acceptedAtUtc));
    }

    public void MarkBound(DateTime boundAtUtc)
    {
        if (Status != QuoteStatus.Accepted)
            throw new InvalidOperationException("Only accepted quotes can be bound.");

        if (boundAtUtc < AcceptedAtUtc)
            throw new InvalidOperationException("Quote cannot be bound before it is accepted.");

        Status = QuoteStatus.Bound;
    }

    public void ClearDomainEvents()
    {
        domainEvents.Clear();
    }

    private static string JoinLines(IReadOnlyCollection<string> values)
    {
        return string.Join("\n", values.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private void EnsureCanReview()
    {
        if (Status != QuoteStatus.Referred)
            throw new InvalidOperationException("Only referred quotes can be reviewed.");
    }

    private static void ValidateReviewInputs(string reviewedByUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reviewedByUserId))
            throw new ArgumentException("Reviewed by user id is required.", nameof(reviewedByUserId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Review reason is required.", nameof(reason));
    }

    private void RecordDecisionSnapshot(
        string reviewedByUserId,
        string reason,
        string? notes,
        DateTime reviewedAtUtc)
    {
        ReviewedByUserId = reviewedByUserId.Trim();
        ReviewedAtUtc = reviewedAtUtc;
        UnderwritingDecisionReason = reason.Trim();
        UnderwritingDecisionNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    private void RecordUnderwritingDecisionEvent(
        QuoteUnderwritingDecision decision,
        DateTime recordedAtUtc)
    {
        domainEvents.Add(new QuoteUnderwritingDecisionRecordedDomainEvent(
            Id,
            SubmissionId,
            OwnerUserId,
            ReviewedByUserId ?? string.Empty,
            decision,
            recordedAtUtc));
    }
}
