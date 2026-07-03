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
    private readonly List<ClaimWorkNote> workNotes = [];
    private readonly List<ClaimInformationRequest> informationRequests = [];
    private readonly List<ClaimDocument> documents = [];
    private readonly List<ClaimReserveChange> reserveChanges = [];

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

    /// <summary>
    /// The adjuster currently working the file. Assignment is a guarded claim (M44.5): the
    /// domain rejects a second adjuster, same-adjuster re-clicks are idempotent, and release is
    /// the explicit hand-over. True races are caught by the <see cref="Version"/> token at save.
    /// </summary>
    public string? AssignedAdjusterUserId { get; private set; }

    public string PolicyNumberAtFiling { get; private set; }

    public DateTime PolicyEffectiveAtFiling { get; private set; }

    public DateTime PolicyExpirationAtFiling { get; private set; }

    public decimal PolicyLimitAtFiling { get; private set; }

    public decimal PolicyRetentionAtFiling { get; private set; }

    /// <summary>What the claimant says the loss is worth (their demand — may exceed the limit).</summary>
    public decimal? ClaimedAmount { get; private set; }

    /// <summary>The insurer's current best-estimate liability, moved only by the assigned adjuster.</summary>
    public decimal ReserveAmount { get; private set; }

    /// <summary>What has actually been paid out; written by the CM5 settlement, zero until then.</summary>
    public decimal PaidAmount { get; private set; }

    public DateTime FiledAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token: every mutation bumps it, and EF Core includes the original
    /// value in the UPDATE's WHERE clause — so racing writers (e.g. two adjusters claiming the
    /// same file in CM2) cannot both win; the loser's save fails loudly.
    /// </summary>
    public long Version { get; private set; }

    public IReadOnlyCollection<ClaimTimelineEntry> TimelineEntries => timelineEntries.AsReadOnly();

    public IReadOnlyCollection<ClaimWorkNote> WorkNotes => workNotes.AsReadOnly();

    public IReadOnlyCollection<ClaimInformationRequest> InformationRequests => informationRequests.AsReadOnly();

    public IReadOnlyCollection<ClaimDocument> Documents => documents.AsReadOnly();

    public IReadOnlyCollection<ClaimReserveChange> ReserveChanges => reserveChanges.AsReadOnly();

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

    /// <summary>
    /// Claims the file for an adjuster. First assignment on a Filed claim also starts the review
    /// (Filed → UnderReview). Same-adjuster re-clicks are idempotent no-ops; a second adjuster is
    /// rejected — release first is the explicit hand-over.
    /// </summary>
    public void AssignTo(string adjusterUserId, DateTime assignedAtUtc)
    {
        ValidateRequiredUserId(adjusterUserId, nameof(adjusterUserId));
        EnsureOpenForAdjusting();

        var trimmedAdjusterUserId = adjusterUserId.Trim();
        if (AssignedAdjusterUserId == trimmedAdjusterUserId)
            return;

        if (AssignedAdjusterUserId is not null)
            throw new InvalidOperationException("This claim is already assigned to another adjuster.");

        AssignedAdjusterUserId = trimmedAdjusterUserId;
        Touch(assignedAtUtc);
        RecordTimeline(
            ClaimTimelineEntryType.AssignmentChanged,
            $"Claim assigned to {AssignedAdjusterUserId}.",
            AssignedAdjusterUserId,
            assignedAtUtc);

        if (Status == ClaimStatus.Filed)
            TransitionTo(ClaimStatus.UnderReview, allowedFrom: [ClaimStatus.Filed], trimmedAdjusterUserId, assignedAtUtc);

        domainEvents.Add(new ClaimAssignedDomainEvent(
            Id,
            ClaimNumber,
            PolicyId,
            OwnerUserId,
            trimmedAdjusterUserId,
            assignedAtUtc));
    }

    /// <summary>Releases the assignment — the explicit hand-over path.</summary>
    public void ReleaseAssignment(string releasedByUserId, DateTime releasedAtUtc)
    {
        ValidateRequiredUserId(releasedByUserId, nameof(releasedByUserId));
        EnsureOpenForAdjusting();

        AssignedAdjusterUserId = null;
        Touch(releasedAtUtc);
        RecordTimeline(
            ClaimTimelineEntryType.AssignmentChanged,
            "Claim assignment released.",
            releasedByUserId,
            releasedAtUtc);
    }

    /// <summary>Appends an internal adjuster work note.</summary>
    public ClaimWorkNote AddWorkNote(string createdByUserId, string note, DateTime createdAtUtc)
    {
        ValidateRequiredUserId(createdByUserId, nameof(createdByUserId));
        EnsureOpenForAdjusting();

        var workNote = ClaimWorkNote.Record(Id, createdByUserId, note, createdAtUtc);
        workNotes.Add(workNote);
        Touch(createdAtUtc);
        RecordTimeline(
            ClaimTimelineEntryType.NoteAdded,
            "Internal adjuster work note added.",
            createdByUserId,
            createdAtUtc);

        return workNote;
    }

    /// <summary>
    /// UnderReview → InformationRequested: the adjuster asks the claimant for more information.
    /// The open request is what the claimant answers via
    /// <see cref="RespondToInformationRequest"/>.
    /// </summary>
    public ClaimInformationRequest RequestInformation(
        string requestedByUserId,
        string title,
        string message,
        DateTime requestedAtUtc)
    {
        ValidateRequiredUserId(requestedByUserId, nameof(requestedByUserId));

        if (Status != ClaimStatus.UnderReview)
            throw new InvalidOperationException("Information can only be requested while a claim is under review.");

        var informationRequest = ClaimInformationRequest.Create(Id, requestedByUserId, title, message, requestedAtUtc);
        informationRequests.Add(informationRequest);
        RecordTimeline(
            ClaimTimelineEntryType.InformationRequested,
            $"Information requested from the claimant: {informationRequest.Title}.",
            requestedByUserId,
            requestedAtUtc);
        TransitionTo(ClaimStatus.InformationRequested, allowedFrom: [ClaimStatus.UnderReview], requestedByUserId, requestedAtUtc);

        domainEvents.Add(new ClaimInformationRequestedDomainEvent(
            Id,
            ClaimNumber,
            informationRequest.Id,
            PolicyId,
            OwnerUserId,
            informationRequest.RequestedByUserId,
            informationRequest.Title,
            requestedAtUtc));

        return informationRequest;
    }

    /// <summary>
    /// The claimant answers an open information request; the claim returns to UnderReview.
    /// The status flips back on the first answer even if other requests remain open — status is
    /// a coarse "whose court is the ball in" flag, and open requests stay visible/answerable.
    /// </summary>
    public void RespondToInformationRequest(
        Guid informationRequestId,
        string respondedByUserId,
        string responseText,
        DateTime respondedAtUtc)
    {
        ValidateRequiredUserId(respondedByUserId, nameof(respondedByUserId));
        EnsureOpenForAdjusting();

        var informationRequest = informationRequests.SingleOrDefault(candidate => candidate.Id == informationRequestId)
            ?? throw new InvalidOperationException("The information request was not found on this claim.");

        informationRequest.Answer(respondedByUserId, responseText, respondedAtUtc);
        Touch(respondedAtUtc);
        RecordTimeline(
            ClaimTimelineEntryType.ClaimantResponded,
            $"Claimant responded to the information request: {informationRequest.Title}.",
            respondedByUserId,
            respondedAtUtc);

        if (Status == ClaimStatus.InformationRequested)
            TransitionTo(ClaimStatus.UnderReview, allowedFrom: [ClaimStatus.InformationRequested], respondedByUserId, respondedAtUtc);

        domainEvents.Add(new ClaimantInformationResponseDomainEvent(
            Id,
            ClaimNumber,
            informationRequest.Id,
            PolicyId,
            OwnerUserId,
            respondedByUserId.Trim(),
            AssignedAdjusterUserId,
            respondedAtUtc));
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

    /// <summary>
    /// Attaches a supporting document (already stored behind the platform storage port; the scan
    /// result is recorded on the returned document by the upload workflow). Only open claims
    /// accept documents; nothing is ever deleted — replacements for rejected scans append.
    /// </summary>
    public ClaimDocument AddDocument(
        ClaimDocumentKind kind,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        string uploadedByUserId,
        DateTime uploadedAtUtc)
    {
        ValidateRequiredUserId(uploadedByUserId, nameof(uploadedByUserId));
        EnsureOpenForAdjusting();

        var document = ClaimDocument.Create(
            Id,
            kind,
            originalFileName,
            contentType,
            sizeBytes,
            storageKey,
            uploadedByUserId,
            uploadedAtUtc);
        documents.Add(document);
        Touch(uploadedAtUtc);
        RecordTimeline(
            ClaimTimelineEntryType.DocumentUploaded,
            $"Supporting document uploaded: {document.OriginalFileName} ({kind}).",
            uploadedByUserId,
            uploadedAtUtc);

        domainEvents.Add(new ClaimDocumentUploadedDomainEvent(
            Id,
            ClaimNumber,
            document.Id,
            PolicyId,
            OwnerUserId,
            kind,
            document.OriginalFileName,
            AssignedAdjusterUserId,
            uploadedAtUtc));

        return document;
    }

    /// <summary>
    /// The claimant declares (or updates) what they are claiming. The demand is not capped —
    /// CM5's settlement guardrail caps what can be *paid*, not what can be asked.
    /// </summary>
    public void SetClaimedAmount(decimal amount, string declaredByUserId, DateTime declaredAtUtc)
    {
        ValidateRequiredUserId(declaredByUserId, nameof(declaredByUserId));
        EnsureOpenForAdjusting();

        if (amount <= 0)
            throw new ArgumentException("Claimed amount must be greater than zero.", nameof(amount));

        var oldAmount = ClaimedAmount;
        ClaimedAmount = amount;
        Touch(declaredAtUtc);
        RecordTimeline(
            ClaimTimelineEntryType.ClaimedAmountUpdated,
            oldAmount is null
                ? $"Claimed amount declared: {FormatMoney(amount)}."
                : $"Claimed amount updated from {FormatMoney(oldAmount.Value)} to {FormatMoney(amount)}.",
            declaredByUserId,
            declaredAtUtc);
    }

    /// <summary>
    /// Sets or adjusts the reserve — a financial commitment, so only the assigned adjuster may
    /// move it, every change requires a reason, and each change appends an audit row. Releasing
    /// to zero is legal; re-stating the same amount is rejected as audit noise.
    /// </summary>
    public void SetReserve(decimal amount, string reason, string adjusterUserId, DateTime changedAtUtc)
    {
        ValidateRequiredUserId(adjusterUserId, nameof(adjusterUserId));
        EnsureOpenForAdjusting();

        if (AssignedAdjusterUserId is null || AssignedAdjusterUserId != adjusterUserId.Trim())
            throw new InvalidOperationException("Only the assigned adjuster can set or adjust the reserve.");

        if (amount < 0)
            throw new ArgumentException("Reserve amount cannot be negative.", nameof(amount));

        if (amount == ReserveAmount)
            throw new InvalidOperationException("The reserve is already at this amount.");

        var oldAmount = ReserveAmount;
        ReserveAmount = amount;
        reserveChanges.Add(ClaimReserveChange.Record(Id, oldAmount, amount, reason, adjusterUserId, changedAtUtc));
        Touch(changedAtUtc);
        RecordTimeline(
            ClaimTimelineEntryType.ReserveChanged,
            $"Reserve changed from {FormatMoney(oldAmount)} to {FormatMoney(amount)}.",
            adjusterUserId,
            changedAtUtc);
    }

    /// <summary>Timeline money is invariant-culture on purpose — the log must not vary by server locale.</summary>
    private static string FormatMoney(decimal amount)
        => amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Adjusting actions are only valid while the claim has no final decision.</summary>
    private void EnsureOpenForAdjusting()
    {
        if (Status is ClaimStatus.Accepted or ClaimStatus.Denied or ClaimStatus.Closed)
            throw new InvalidOperationException(
                $"A claim in status {Status} can no longer be worked.");
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
