namespace LIAnsureProtect.Domain.Quotes;

public sealed class QuoteReferralTimelineEntry
{
    private QuoteReferralTimelineEntry(
        Guid id,
        Guid quoteReferralOperationId,
        Guid quoteId,
        ReferralTimelineEntryType entryType,
        string summary,
        string createdByUserId,
        DateTime createdAtUtc)
    {
        Id = id;
        QuoteReferralOperationId = quoteReferralOperationId;
        QuoteId = quoteId;
        EntryType = entryType;
        Summary = summary;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    private QuoteReferralTimelineEntry()
    {
        Summary = string.Empty;
        CreatedByUserId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid QuoteReferralOperationId { get; private set; }

    public Guid QuoteId { get; private set; }

    public ReferralTimelineEntryType EntryType { get; private set; }

    public string Summary { get; private set; }

    public string CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    internal static QuoteReferralTimelineEntry Record(
        Guid operationId,
        Guid quoteId,
        ReferralTimelineEntryType entryType,
        string summary,
        string createdByUserId,
        DateTime createdAtUtc)
    {
        return new QuoteReferralTimelineEntry(
            Guid.NewGuid(),
            operationId,
            quoteId,
            entryType,
            summary.Trim(),
            createdByUserId.Trim(),
            createdAtUtc);
    }
}
