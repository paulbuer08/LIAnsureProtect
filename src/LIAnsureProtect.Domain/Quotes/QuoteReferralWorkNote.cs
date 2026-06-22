namespace LIAnsureProtect.Domain.Quotes;

public sealed class QuoteReferralWorkNote
{
    private QuoteReferralWorkNote(
        Guid id,
        Guid quoteReferralOperationId,
        Guid quoteId,
        string note,
        string createdByUserId,
        DateTime createdAtUtc)
    {
        Id = id;
        QuoteReferralOperationId = quoteReferralOperationId;
        QuoteId = quoteId;
        Note = note;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    private QuoteReferralWorkNote()
    {
        Note = string.Empty;
        CreatedByUserId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid QuoteReferralOperationId { get; private set; }

    public Guid QuoteId { get; private set; }

    public string Note { get; private set; }

    public string CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    internal static QuoteReferralWorkNote Record(
        Guid operationId,
        Guid quoteId,
        string createdByUserId,
        string note,
        DateTime createdAtUtc)
    {
        return new QuoteReferralWorkNote(
            Guid.NewGuid(),
            operationId,
            quoteId,
            note.Trim(),
            createdByUserId.Trim(),
            createdAtUtc);
    }
}
