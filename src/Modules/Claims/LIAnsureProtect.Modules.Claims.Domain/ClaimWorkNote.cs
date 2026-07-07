namespace LIAnsureProtect.Modules.Claims.Domain;

/// <summary>
/// One append-only internal adjuster work note on a claim. Created only through the
/// <see cref="Claim"/> aggregate; never edited or deleted.
/// </summary>
public sealed class ClaimWorkNote
{
    private ClaimWorkNote()
    {
        Note = string.Empty;
        CreatedByUserId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid ClaimId { get; private set; }

    public string Note { get; private set; }

    public string CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    internal static ClaimWorkNote Record(
        Guid claimId,
        string createdByUserId,
        string note,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("Work note is required.", nameof(note));

        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("User id is required.", nameof(createdByUserId));

        return new ClaimWorkNote
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            Note = note.Trim(),
            CreatedByUserId = createdByUserId.Trim(),
            CreatedAtUtc = createdAtUtc
        };
    }
}
