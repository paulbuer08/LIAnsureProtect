namespace LIAnsureProtect.Modules.Claims.Domain;

/// <summary>
/// One append-only reserve change: the before/after amounts, the mandatory reason, who and when.
/// This log — not the current <see cref="Claim.ReserveAmount"/> — is what auditors and
/// reinsurers read. Created only through <see cref="Claim.SetReserve"/>.
/// </summary>
public sealed class ClaimReserveChange
{
    private ClaimReserveChange()
    {
        Reason = string.Empty;
        ChangedByUserId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid ClaimId { get; private set; }

    public decimal OldAmount { get; private set; }

    public decimal NewAmount { get; private set; }

    public string Reason { get; private set; }

    public string ChangedByUserId { get; private set; }

    public DateTime ChangedAtUtc { get; private set; }

    internal static ClaimReserveChange Record(
        Guid claimId,
        decimal oldAmount,
        decimal newAmount,
        string reason,
        string changedByUserId,
        DateTime changedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reserve change requires a reason.", nameof(reason));

        if (string.IsNullOrWhiteSpace(changedByUserId))
            throw new ArgumentException("User id is required.", nameof(changedByUserId));

        return new ClaimReserveChange
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            OldAmount = oldAmount,
            NewAmount = newAmount,
            Reason = reason.Trim(),
            ChangedByUserId = changedByUserId.Trim(),
            ChangedAtUtc = changedAtUtc
        };
    }
}
