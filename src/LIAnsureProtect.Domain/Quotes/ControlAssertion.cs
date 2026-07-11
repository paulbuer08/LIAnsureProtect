namespace LIAnsureProtect.Domain.Quotes;

public enum ControlType
{
    MultiFactorAuthentication,
    EndpointDetectionAndResponse,
    BackupRecovery,
    IncidentResponsePlan,
    SensitiveData
}

public enum ControlAssuranceState
{
    SelfAttested,
    EvidenceProvided,
    MachineVerified,
    HumanVerified,
    Rejected,
    Expired
}

public sealed class ControlAssertion
{
    private ControlAssertion()
    {
        ClaimedState = string.Empty;
        EvidenceReason = string.Empty;
        DetailsJson = "{}";
    }

    public Guid Id { get; private set; }

    public Guid QuoteId { get; private set; }

    public int QuoteVersion { get; private set; }

    public ControlType ControlType { get; private set; }

    public string ClaimedState { get; private set; }

    public ControlAssuranceState AssuranceState { get; private set; }

    public bool EvidenceRequired { get; private set; }

    public string EvidenceReason { get; private set; }

    public string DetailsJson { get; private set; }

    public DateTime CapturedAtUtc { get; private set; }

    public string? VerifiedByUserId { get; private set; }

    public DateTime? VerifiedAtUtc { get; private set; }

    public static ControlAssertion Create(
        Guid quoteId,
        int quoteVersion,
        ControlType controlType,
        string claimedState,
        bool evidenceRequired,
        string evidenceReason,
        DateTime capturedAtUtc,
        string? detailsJson = null)
    {
        if (quoteId == Guid.Empty)
            throw new ArgumentException("Quote id is required.", nameof(quoteId));

        ArgumentOutOfRangeException.ThrowIfLessThan(quoteVersion, 1);

        if (string.IsNullOrWhiteSpace(claimedState))
            throw new ArgumentException("Claimed state is required.", nameof(claimedState));

        if (evidenceRequired && string.IsNullOrWhiteSpace(evidenceReason))
            throw new ArgumentException("Evidence reason is required when evidence is required.", nameof(evidenceReason));

        return new ControlAssertion
        {
            Id = Guid.NewGuid(),
            QuoteId = quoteId,
            QuoteVersion = quoteVersion,
            ControlType = controlType,
            ClaimedState = claimedState.Trim(),
            AssuranceState = ControlAssuranceState.SelfAttested,
            EvidenceRequired = evidenceRequired,
            EvidenceReason = evidenceReason.Trim(),
            DetailsJson = string.IsNullOrWhiteSpace(detailsJson) ? "{}" : detailsJson,
            CapturedAtUtc = capturedAtUtc
        };
    }

    public void RecordHumanVerification(string verifiedByUserId, bool satisfied, DateTime verifiedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(verifiedByUserId))
            throw new ArgumentException("Verified-by user id is required.", nameof(verifiedByUserId));

        AssuranceState = satisfied
            ? ControlAssuranceState.HumanVerified
            : ControlAssuranceState.Rejected;
        VerifiedByUserId = verifiedByUserId.Trim();
        VerifiedAtUtc = verifiedAtUtc;
    }
}
