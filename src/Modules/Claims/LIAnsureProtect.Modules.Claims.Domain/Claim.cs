using LIAnsureProtect.Platform.Abstractions.DomainEvents;

namespace LIAnsureProtect.Modules.Claims.Domain;

/// <summary>
/// The Claims context's aggregate root: one filed cyber claim against a bound policy.
/// <para>
/// The policy is referenced <b>by id only</b> (no cross-schema FK); the policy facts that matter
/// for adjudication (number, period, limit, retention) are snapshotted at filing time — the same
/// discipline as the bind-time snapshots on the legacy <c>Policy</c> — so later policy-side
/// changes can never alter what this claim is judged against.
/// </para>
/// <para>
/// The status lifecycle is domain-enforced: Filed → UnderReview → InformationRequested (and back)
/// → Accepted/Denied → Closed. Every mutation appends a timeline entry and bumps the
/// optimistic-concurrency <see cref="Version"/> token (the M44.5 pattern).
/// </para>
/// </summary>
public sealed class Claim : IHasDomainEvents
{
    private readonly List<IDomainEvent> domainEvents = [];
    private readonly List<ClaimTimelineEntry> timelineEntries = [];

    // The only constructor: EF Core materializes through it, and the File factory assigns state
    // via the private property setters.
    private Claim()
    {
        OwnerUserId = string.Empty;
        ClaimNumber = string.Empty;
        Description = string.Empty;
        PolicyNumberAtFiling = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid PolicyId { get; private set; }

    public Guid SubmissionId { get; private set; }

    public string OwnerUserId { get; private set; }

    public string ClaimNumber { get; private set; }

    public ClaimIncidentType IncidentType { get; private set; }

    public DateTime IncidentAtUtc { get; private set; }

    public DateTime DiscoveredAtUtc { get; private set; }

    public string Description { get; private set; }

    public ClaimStatus Status { get; private set; }

    public string PolicyNumberAtFiling { get; private set; }

    public DateTime PolicyEffectiveAtFiling { get; private set; }

    public DateTime PolicyExpirationAtFiling { get; private set; }

    public decimal PolicyLimitAtFiling { get; private set; }

    public decimal PolicyRetentionAtFiling { get; private set; }

    public DateTime FiledAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token: every mutation bumps it, and EF Core includes the original
    /// value in the UPDATE's WHERE clause — so racing writers (e.g. two adjusters claiming the
    /// same file in CM2) cannot both win; the loser's save fails loudly.
    /// </summary>
    public long Version { get; private set; }

    public IReadOnlyCollection<ClaimTimelineEntry> TimelineEntries => timelineEntries.AsReadOnly();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();

    public static Claim File(
        Guid policyId,
        Guid submissionId,
        string ownerUserId,
        string claimNumber,
        ClaimIncidentType incidentType,
        DateTime incidentAtUtc,
        DateTime discoveredAtUtc,
        string description,
        string policyNumberAtFiling,
        DateTime policyEffectiveAtFiling,
        DateTime policyExpirationAtFiling,
        decimal policyLimitAtFiling,
        decimal policyRetentionAtFiling,
        DateTime filedAtUtc)
    {
        if (policyId == Guid.Empty)
            throw new ArgumentException("Policy id is required.", nameof(policyId));

        ValidateRequiredUserId(ownerUserId, nameof(ownerUserId));

        if (string.IsNullOrWhiteSpace(claimNumber))
            throw new ArgumentException("Claim number is required.", nameof(claimNumber));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Incident description is required.", nameof(description));

        if (string.IsNullOrWhiteSpace(policyNumberAtFiling))
            throw new ArgumentException("Policy number is required.", nameof(policyNumberAtFiling));

        if (discoveredAtUtc < incidentAtUtc)
            throw new InvalidOperationException("Discovery date cannot be before the incident date.");

        if (incidentAtUtc < policyEffectiveAtFiling || incidentAtUtc > policyExpirationAtFiling)
            throw new InvalidOperationException("Incident date must fall within the policy period.");

        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = policyId,
            SubmissionId = submissionId,
            OwnerUserId = ownerUserId.Trim(),
            ClaimNumber = claimNumber.Trim(),
            IncidentType = incidentType,
            IncidentAtUtc = incidentAtUtc,
            DiscoveredAtUtc = discoveredAtUtc,
            Description = description.Trim(),
            Status = ClaimStatus.Filed,
            PolicyNumberAtFiling = policyNumberAtFiling.Trim(),
            PolicyEffectiveAtFiling = policyEffectiveAtFiling,
            PolicyExpirationAtFiling = policyExpirationAtFiling,
            PolicyLimitAtFiling = policyLimitAtFiling,
            PolicyRetentionAtFiling = policyRetentionAtFiling,
            FiledAtUtc = filedAtUtc,
            UpdatedAtUtc = filedAtUtc
        };

        claim.RecordTimeline(
            ClaimTimelineEntryType.ClaimFiled,
            $"Claim {claim.ClaimNumber} filed for {incidentType} against policy {claim.PolicyNumberAtFiling}.",
            claim.OwnerUserId,
            filedAtUtc);

        claim.domainEvents.Add(new ClaimFiledDomainEvent(
            claim.Id,
            claim.ClaimNumber,
            claim.PolicyId,
            claim.PolicyNumberAtFiling,
            claim.OwnerUserId,
            claim.IncidentType,
            filedAtUtc));

        return claim;
    }

    /// <summary>Filed → UnderReview (an adjuster starts working the claim — wired in CM2).</summary>
    public void StartReview(string startedByUserId, DateTime startedAtUtc)
    {
        ValidateRequiredUserId(startedByUserId, nameof(startedByUserId));
        TransitionTo(ClaimStatus.UnderReview, allowedFrom: [ClaimStatus.Filed], startedByUserId, startedAtUtc);
    }

    /// <summary>UnderReview → InformationRequested (the adjuster needs more from the claimant).</summary>
    public void RequestInformation(string requestedByUserId, DateTime requestedAtUtc)
    {
        ValidateRequiredUserId(requestedByUserId, nameof(requestedByUserId));
        TransitionTo(ClaimStatus.InformationRequested, allowedFrom: [ClaimStatus.UnderReview], requestedByUserId, requestedAtUtc);
    }

    /// <summary>InformationRequested → UnderReview (the claimant responded).</summary>
    public void RecordClaimantResponse(string respondedByUserId, DateTime respondedAtUtc)
    {
        ValidateRequiredUserId(respondedByUserId, nameof(respondedByUserId));
        TransitionTo(ClaimStatus.UnderReview, allowedFrom: [ClaimStatus.InformationRequested], respondedByUserId, respondedAtUtc);
    }

    /// <summary>UnderReview → Accepted (a favorable decision — wired with guardrails in CM5).</summary>
    public void Accept(string decidedByUserId, DateTime decidedAtUtc)
    {
        ValidateRequiredUserId(decidedByUserId, nameof(decidedByUserId));
        TransitionTo(ClaimStatus.Accepted, allowedFrom: [ClaimStatus.UnderReview], decidedByUserId, decidedAtUtc);
    }

    /// <summary>UnderReview → Denied (an unfavorable decision — wired with guardrails in CM5).</summary>
    public void Deny(string decidedByUserId, DateTime decidedAtUtc)
    {
        ValidateRequiredUserId(decidedByUserId, nameof(decidedByUserId));
        TransitionTo(ClaimStatus.Denied, allowedFrom: [ClaimStatus.UnderReview], decidedByUserId, decidedAtUtc);
    }

    /// <summary>Accepted/Denied → Closed (the file is finished).</summary>
    public void Close(string closedByUserId, DateTime closedAtUtc)
    {
        ValidateRequiredUserId(closedByUserId, nameof(closedByUserId));
        TransitionTo(ClaimStatus.Closed, allowedFrom: [ClaimStatus.Accepted, ClaimStatus.Denied], closedByUserId, closedAtUtc);
    }

    public void ClearDomainEvents()
    {
        domainEvents.Clear();
    }

    private void TransitionTo(
        ClaimStatus target,
        ClaimStatus[] allowedFrom,
        string changedByUserId,
        DateTime changedAtUtc)
    {
        if (!allowedFrom.Contains(Status))
            throw new InvalidOperationException(
                $"A claim in status {Status} cannot transition to {target}.");

        var oldStatus = Status;
        Status = target;
        Touch(changedAtUtc);
        RecordTimeline(
            ClaimTimelineEntryType.StatusChanged,
            $"Status changed from {oldStatus} to {Status}.",
            changedByUserId,
            changedAtUtc);
    }

    private void RecordTimeline(
        ClaimTimelineEntryType entryType,
        string summary,
        string createdByUserId,
        DateTime createdAtUtc)
    {
        timelineEntries.Add(ClaimTimelineEntry.Record(Id, entryType, summary, createdByUserId, createdAtUtc));
    }

    /// <summary>Marks a mutation: refreshes the activity timestamp and bumps the concurrency token.</summary>
    private void Touch(DateTime updatedAtUtc)
    {
        UpdatedAtUtc = updatedAtUtc;
        Version++;
    }

    private static void ValidateRequiredUserId(string userId, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required.", parameterName);
    }
}
