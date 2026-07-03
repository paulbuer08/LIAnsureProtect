namespace LIAnsureProtect.Modules.Claims.Domain;

/// <summary>
/// One append-only entry in a claim's timeline. Entries are only created through the
/// <see cref="Claim"/> aggregate and are never edited or deleted.
/// </summary>
public sealed class ClaimTimelineEntry
{
    private ClaimTimelineEntry()
    {
        Summary = string.Empty;
        CreatedByUserId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid ClaimId { get; private set; }

    public ClaimTimelineEntryType EntryType { get; private set; }

    public string Summary { get; private set; }

    public string CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    internal static ClaimTimelineEntry Record(
        Guid claimId,
        ClaimTimelineEntryType entryType,
        string summary,
        string createdByUserId,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Timeline summary is required.", nameof(summary));

        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("User id is required.", nameof(createdByUserId));

        return new ClaimTimelineEntry
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            EntryType = entryType,
            Summary = summary.Trim(),
            CreatedByUserId = createdByUserId.Trim(),
            CreatedAtUtc = createdAtUtc
        };
    }
}
