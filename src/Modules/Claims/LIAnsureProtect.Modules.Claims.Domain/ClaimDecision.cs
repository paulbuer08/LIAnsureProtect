namespace LIAnsureProtect.Modules.Claims.Domain;

public enum ClaimDecisionOutcome
{
    Accepted,
    Denied,
    Closed
}

/// <summary>
/// One append-only decision audit row: the outcome, its money, its reasons, and a snapshot of the
/// claimed/reserve amounts at decision time — the compliance-grade "who decided what, when, and
/// why" record. Created only through the <see cref="Claim"/> decision methods.
/// </summary>
public sealed class ClaimDecision
{
    private ClaimDecision()
    {
        Reason = string.Empty;
        DecidedByUserId = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid ClaimId { get; private set; }

    public ClaimDecisionOutcome Outcome { get; private set; }

    public decimal? SettlementAmount { get; private set; }

    public ClaimDenialReason? DenialReason { get; private set; }

    public string Reason { get; private set; }

    public string? Notes { get; private set; }

    public decimal? ClaimedAmountAtDecision { get; private set; }

    public decimal ReserveAmountAtDecision { get; private set; }

    public string DecidedByUserId { get; private set; }

    public DateTime DecidedAtUtc { get; private set; }

    internal static ClaimDecision Record(
        Claim claim,
        ClaimDecisionOutcome outcome,
        decimal? settlementAmount,
        ClaimDenialReason? denialReason,
        string reason,
        string? notes,
        string decidedByUserId,
        DateTime decidedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(claim);

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A decision reason is required.", nameof(reason));

        if (string.IsNullOrWhiteSpace(decidedByUserId))
            throw new ArgumentException("User id is required.", nameof(decidedByUserId));

        return new ClaimDecision
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            Outcome = outcome,
            SettlementAmount = settlementAmount,
            DenialReason = denialReason,
            Reason = reason.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            ClaimedAmountAtDecision = claim.ClaimedAmount,
            ReserveAmountAtDecision = claim.ReserveAmount,
            DecidedByUserId = decidedByUserId.Trim(),
            DecidedAtUtc = decidedAtUtc
        };
    }
}
