using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes.Assurance;

public sealed record ControlAssertionDecision(
    ControlType ControlType,
    string ClaimedState,
    bool EvidenceRequired,
    string EvidenceReason);

public static class ControlAssurancePolicy
{
    public const string AttestationWordingVersion = "2026-07-12-v1";

    public static IReadOnlyList<ControlAssertionDecision> Evaluate(CreateQuoteAssuranceInput input)
    {
        var materialLimit = input.RequestedLimit >= 1_000_000m;
        var priorLoss = input.PriorCyberIncidents > 0;

        return
        [
            Decide(
                ControlType.MultiFactorAuthentication,
                input.MfaStatus.ToString(),
                input.MfaStatus == CyberSecurityControlStatus.Implemented,
                "MFA is claimed as implemented and receives material rating credit."),
            Decide(
                ControlType.EndpointDetectionAndResponse,
                input.EdrStatus.ToString(),
                input.EdrStatus == CyberSecurityControlStatus.Implemented && (materialLimit || priorLoss),
                "EDR is claimed as implemented for a materially exposed risk."),
            Decide(
                ControlType.BackupRecovery,
                input.BackupMaturity.ToString(),
                input.BackupMaturity == BackupMaturity.Mature,
                "Mature backup and recovery is claimed and receives material rating credit."),
            Decide(
                ControlType.IncidentResponsePlan,
                input.HasIncidentResponsePlan ? "InPlace" : "NotInPlace",
                input.HasIncidentResponsePlan,
                "An incident response plan is claimed in place and receives rating credit."),
            Decide(
                ControlType.SensitiveData,
                input.SensitiveDataExposure.ToString(),
                input.SensitiveDataExposure == SensitiveDataExposure.Low && (materialLimit || priorLoss),
                "Low sensitive-data exposure is claimed for a materially exposed risk.")
        ];
    }

    private static ControlAssertionDecision Decide(
        ControlType controlType,
        string claimedState,
        bool evidenceRequired,
        string reason)
    {
        return new ControlAssertionDecision(
            controlType,
            claimedState,
            evidenceRequired,
            evidenceRequired ? reason : string.Empty);
    }
}

public sealed record CreateQuoteAssuranceInput(
    decimal RequestedLimit,
    CyberSecurityControlStatus MfaStatus,
    CyberSecurityControlStatus EdrStatus,
    BackupMaturity BackupMaturity,
    bool HasIncidentResponsePlan,
    int PriorCyberIncidents,
    SensitiveDataExposure SensitiveDataExposure);
