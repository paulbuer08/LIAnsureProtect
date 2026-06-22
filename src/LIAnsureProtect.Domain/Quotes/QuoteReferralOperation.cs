namespace LIAnsureProtect.Domain.Quotes;

public sealed class QuoteReferralOperation
{
    private const string SystemUserId = "system";

    private readonly List<QuoteReferralWorkNote> notes = [];
    private readonly List<QuoteReferralFollowUpTask> tasks = [];
    private readonly List<QuoteReferralTimelineEntry> timelineEntries = [];

    private QuoteReferralOperation(
        Guid id,
        Guid quoteId,
        ReferralPriority priority,
        ReferralOperationStatus status,
        DateTime dueAtUtc,
        DateTime createdAtUtc)
    {
        Id = id;
        QuoteId = quoteId;
        Priority = priority;
        Status = status;
        DueAtUtc = dueAtUtc;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    private QuoteReferralOperation()
    {
    }

    public Guid Id { get; private set; }

    public Guid QuoteId { get; private set; }

    public string? AssignedUnderwriterUserId { get; private set; }

    public ReferralPriority Priority { get; private set; }

    public ReferralOperationStatus Status { get; private set; }

    public DateTime DueAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? ClosedAtUtc { get; private set; }

    public IReadOnlyCollection<QuoteReferralWorkNote> Notes => notes.AsReadOnly();

    public IReadOnlyCollection<QuoteReferralFollowUpTask> Tasks => tasks.AsReadOnly();

    public IReadOnlyCollection<QuoteReferralTimelineEntry> TimelineEntries => timelineEntries.AsReadOnly();

    public static QuoteReferralOperation CreateDefault(
        Guid quoteId,
        CyberRiskTier riskTier,
        DateTime referredAtUtc,
        DateTime quoteExpiresAtUtc)
    {
        if (quoteId == Guid.Empty)
            throw new ArgumentException("Quote id is required.", nameof(quoteId));

        if (quoteExpiresAtUtc < referredAtUtc)
            throw new InvalidOperationException("Referral due date cannot be calculated after quote expiry.");

        var priority = riskTier is CyberRiskTier.High or CyberRiskTier.Severe
            ? ReferralPriority.High
            : ReferralPriority.Normal;
        var targetDueAtUtc = referredAtUtc.AddDays(priority == ReferralPriority.High ? 2 : 5);
        var dueAtUtc = targetDueAtUtc <= quoteExpiresAtUtc ? targetDueAtUtc : quoteExpiresAtUtc;
        var operation = new QuoteReferralOperation(
            Guid.NewGuid(),
            quoteId,
            priority,
            ReferralOperationStatus.New,
            dueAtUtc,
            referredAtUtc);

        operation.RecordTimeline(
            ReferralTimelineEntryType.OperationCreated,
            $"Referral operations created with {priority} priority and due date {dueAtUtc:O}.",
            SystemUserId,
            referredAtUtc);

        return operation;
    }

    public void AssignTo(string assignedUnderwriterUserId, DateTime assignedAtUtc)
    {
        EnsureOpen();
        ValidateRequiredUserId(assignedUnderwriterUserId, nameof(assignedUnderwriterUserId));

        AssignedUnderwriterUserId = assignedUnderwriterUserId.Trim();
        Status = Status == ReferralOperationStatus.New ? ReferralOperationStatus.InReview : Status;
        UpdatedAtUtc = assignedAtUtc;
        RecordTimeline(
            ReferralTimelineEntryType.AssignmentChanged,
            $"Referral assigned to {AssignedUnderwriterUserId}.",
            AssignedUnderwriterUserId,
            assignedAtUtc);
    }

    public void ReleaseAssignment(string releasedByUserId, DateTime releasedAtUtc)
    {
        EnsureOpen();
        ValidateRequiredUserId(releasedByUserId, nameof(releasedByUserId));

        AssignedUnderwriterUserId = null;
        UpdatedAtUtc = releasedAtUtc;
        RecordTimeline(
            ReferralTimelineEntryType.AssignmentChanged,
            "Referral assignment released.",
            releasedByUserId,
            releasedAtUtc);
    }

    public void Triage(
        string changedByUserId,
        ReferralPriority priority,
        ReferralOperationStatus status,
        DateTime dueAtUtc,
        DateTime changedAtUtc)
    {
        EnsureOpen();
        ValidateRequiredUserId(changedByUserId, nameof(changedByUserId));

        if (status == ReferralOperationStatus.Closed)
            throw new InvalidOperationException("Use final underwriting decision actions to close referral operations.");

        if (dueAtUtc < CreatedAtUtc)
            throw new InvalidOperationException("Referral due date cannot be before operation creation.");

        var oldPriority = Priority;
        var oldStatus = Status;
        var oldDueAtUtc = DueAtUtc;

        Priority = priority;
        Status = status;
        DueAtUtc = dueAtUtc;
        UpdatedAtUtc = changedAtUtc;

        if (oldPriority != Priority)
            RecordTimeline(ReferralTimelineEntryType.PriorityChanged, $"Priority changed from {oldPriority} to {Priority}.", changedByUserId, changedAtUtc);

        if (oldStatus != Status)
            RecordTimeline(ReferralTimelineEntryType.StatusChanged, $"Status changed from {oldStatus} to {Status}.", changedByUserId, changedAtUtc);

        if (oldDueAtUtc != DueAtUtc)
            RecordTimeline(ReferralTimelineEntryType.DueDateChanged, $"Due date changed from {oldDueAtUtc:O} to {DueAtUtc:O}.", changedByUserId, changedAtUtc);
    }

    public QuoteReferralWorkNote AddNote(
        string createdByUserId,
        string note,
        DateTime createdAtUtc)
    {
        EnsureOpen();
        ValidateRequiredUserId(createdByUserId, nameof(createdByUserId));

        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("Work note is required.", nameof(note));

        var workNote = QuoteReferralWorkNote.Record(Id, QuoteId, createdByUserId, note, createdAtUtc);
        notes.Add(workNote);
        UpdatedAtUtc = createdAtUtc;
        RecordTimeline(
            ReferralTimelineEntryType.NoteAdded,
            "Internal underwriting work note added.",
            createdByUserId,
            createdAtUtc);

        return workNote;
    }

    public QuoteReferralFollowUpTask AddTask(
        string createdByUserId,
        string title,
        DateTime dueAtUtc,
        DateTime createdAtUtc)
    {
        EnsureOpen();
        ValidateRequiredUserId(createdByUserId, nameof(createdByUserId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Follow-up task title is required.", nameof(title));

        if (dueAtUtc < createdAtUtc)
            throw new InvalidOperationException("Follow-up task due date cannot be before task creation.");

        var task = QuoteReferralFollowUpTask.Create(Id, QuoteId, createdByUserId, title, dueAtUtc, createdAtUtc);
        tasks.Add(task);
        UpdatedAtUtc = createdAtUtc;
        RecordTimeline(
            ReferralTimelineEntryType.TaskAdded,
            $"Follow-up task added: {task.Title}.",
            createdByUserId,
            createdAtUtc);

        return task;
    }

    public void CompleteTask(Guid taskId, string completedByUserId, DateTime completedAtUtc)
    {
        EnsureOpen();
        ValidateRequiredUserId(completedByUserId, nameof(completedByUserId));

        var task = tasks.SingleOrDefault(candidate => candidate.Id == taskId)
            ?? throw new InvalidOperationException("Follow-up task was not found.");

        task.Complete(completedByUserId, completedAtUtc);
        UpdatedAtUtc = completedAtUtc;
        RecordTimeline(
            ReferralTimelineEntryType.TaskCompleted,
            $"Follow-up task completed: {task.Title}.",
            completedByUserId,
            completedAtUtc);
    }

    public void CloseForDecision(
        string reviewedByUserId,
        QuoteUnderwritingDecision decision,
        DateTime closedAtUtc)
    {
        EnsureOpen();
        ValidateRequiredUserId(reviewedByUserId, nameof(reviewedByUserId));

        var oldStatus = Status;
        Status = ReferralOperationStatus.Closed;
        ClosedAtUtc = closedAtUtc;
        UpdatedAtUtc = closedAtUtc;
        RecordTimeline(
            ReferralTimelineEntryType.StatusChanged,
            $"Status changed from {oldStatus} to {Status} because final underwriting decision {decision} was recorded.",
            reviewedByUserId,
            closedAtUtc);
    }

    public void RecordEvidenceRequestCreated(
        Guid evidenceRequestId,
        string createdByUserId,
        DateTime createdAtUtc)
    {
        EnsureOpen();
        ValidateRequiredUserId(createdByUserId, nameof(createdByUserId));

        var oldStatus = Status;
        Status = ReferralOperationStatus.WaitingForInformation;
        UpdatedAtUtc = createdAtUtc;
        RecordTimeline(
            ReferralTimelineEntryType.EvidenceRequestCreated,
            $"Evidence request {evidenceRequestId} created; referral is waiting for information.",
            createdByUserId,
            createdAtUtc);

        if (oldStatus != Status)
            RecordTimeline(
                ReferralTimelineEntryType.StatusChanged,
                $"Status changed from {oldStatus} to {Status} because evidence was requested.",
                createdByUserId,
                createdAtUtc);
    }

    public void RecordEvidenceRequestResponded(
        Guid evidenceRequestId,
        string respondedByUserId,
        DateTime respondedAtUtc)
    {
        EnsureOpen();
        ValidateRequiredUserId(respondedByUserId, nameof(respondedByUserId));

        UpdatedAtUtc = respondedAtUtc;
        RecordTimeline(
            ReferralTimelineEntryType.EvidenceRequestResponded,
            $"Evidence request {evidenceRequestId} received a customer or broker response.",
            respondedByUserId,
            respondedAtUtc);
    }

    public void RecordEvidenceRequestAccepted(
        Guid evidenceRequestId,
        string acceptedByUserId,
        DateTime acceptedAtUtc)
    {
        EnsureOpen();
        ValidateRequiredUserId(acceptedByUserId, nameof(acceptedByUserId));

        UpdatedAtUtc = acceptedAtUtc;
        RecordTimeline(
            ReferralTimelineEntryType.EvidenceRequestAccepted,
            $"Evidence request {evidenceRequestId} accepted by underwriting.",
            acceptedByUserId,
            acceptedAtUtc);
    }

    public void RecordEvidenceRequestCancelled(
        Guid evidenceRequestId,
        string cancelledByUserId,
        DateTime cancelledAtUtc)
    {
        EnsureOpen();
        ValidateRequiredUserId(cancelledByUserId, nameof(cancelledByUserId));

        UpdatedAtUtc = cancelledAtUtc;
        RecordTimeline(
            ReferralTimelineEntryType.EvidenceRequestCancelled,
            $"Evidence request {evidenceRequestId} cancelled by underwriting.",
            cancelledByUserId,
            cancelledAtUtc);
    }

    public void RecordEvidenceRequestFollowUpSent(
        Guid evidenceRequestId,
        string followedUpByUserId,
        DateTime followedUpAtUtc)
    {
        EnsureOpen();
        ValidateRequiredUserId(followedUpByUserId, nameof(followedUpByUserId));

        UpdatedAtUtc = followedUpAtUtc;
        RecordTimeline(
            ReferralTimelineEntryType.EvidenceRequestFollowUpSent,
            $"Evidence request {evidenceRequestId} follow-up reminder sent.",
            followedUpByUserId,
            followedUpAtUtc);
    }

    private void RecordTimeline(
        ReferralTimelineEntryType entryType,
        string summary,
        string createdByUserId,
        DateTime createdAtUtc)
    {
        timelineEntries.Add(QuoteReferralTimelineEntry.Record(
            Id,
            QuoteId,
            entryType,
            summary,
            createdByUserId,
            createdAtUtc));
    }

    private void EnsureOpen()
    {
        if (Status == ReferralOperationStatus.Closed)
            throw new InvalidOperationException("Referral operations are closed.");
    }

    private static void ValidateRequiredUserId(string userId, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required.", parameterName);
    }
}
