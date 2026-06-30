namespace LIAnsureProtect.Modules.Underwriting.Domain.Referrals;

public sealed class QuoteReferralFollowUpTask
{
    private QuoteReferralFollowUpTask(
        Guid id,
        Guid quoteReferralOperationId,
        Guid quoteId,
        string title,
        DateTime dueAtUtc,
        string createdByUserId,
        DateTime createdAtUtc)
    {
        Id = id;
        QuoteReferralOperationId = quoteReferralOperationId;
        QuoteId = quoteId;
        Title = title;
        DueAtUtc = dueAtUtc;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    private QuoteReferralFollowUpTask()
    {
        Title = string.Empty;
        CreatedByUserId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid QuoteReferralOperationId { get; private set; }

    public Guid QuoteId { get; private set; }

    public string Title { get; private set; }

    public DateTime DueAtUtc { get; private set; }

    public string CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public string? CompletedByUserId { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public bool IsCompleted => CompletedAtUtc is not null;

    internal static QuoteReferralFollowUpTask Create(
        Guid operationId,
        Guid quoteId,
        string createdByUserId,
        string title,
        DateTime dueAtUtc,
        DateTime createdAtUtc)
    {
        return new QuoteReferralFollowUpTask(
            Guid.NewGuid(),
            operationId,
            quoteId,
            title.Trim(),
            dueAtUtc,
            createdByUserId.Trim(),
            createdAtUtc);
    }

    internal void Complete(string completedByUserId, DateTime completedAtUtc)
    {
        if (IsCompleted)
            throw new InvalidOperationException("Follow-up task is already completed.");

        if (string.IsNullOrWhiteSpace(completedByUserId))
            throw new ArgumentException("Completed by user id is required.", nameof(completedByUserId));

        CompletedByUserId = completedByUserId.Trim();
        CompletedAtUtc = completedAtUtc;
    }
}
