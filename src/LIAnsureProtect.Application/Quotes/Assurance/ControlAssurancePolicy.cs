using LIAnsureProtect.Application.Common.Exceptions;
using LIAnsureProtect.Domain.Quotes;
using System.Text.Json;

namespace LIAnsureProtect.Application.Quotes.Assurance;

public sealed record ControlAssertionDecision(
    ControlType ControlType,
    string ClaimedState,
    bool EvidenceRequired,
    string EvidenceReason,
    string DetailsJson);

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
                "MFA is claimed as implemented and receives material rating credit.",
                new
                {
                    input.ControlDetails?.MfaCoversPrivilegedAccess,
                    input.ControlDetails?.MfaCoversEmail,
                    input.ControlDetails?.MfaCoversRemoteAccess,
                    input.ControlDetails?.MfaCoversWorkforce,
                    input.ControlDetails?.MfaPhishingResistant
                }),
            Decide(
                ControlType.EndpointDetectionAndResponse,
                input.EdrStatus.ToString(),
                input.EdrStatus == CyberSecurityControlStatus.Implemented && (materialLimit || priorLoss),
                "EDR is claimed as implemented for a materially exposed risk.",
                new
                {
                    input.ControlDetails?.EdrCoveragePercent,
                    input.ControlDetails?.EdrCoversServers,
                    input.ControlDetails?.EdrActivelyMonitored,
                    input.ControlDetails?.EdrTamperProtection
                }),
            Decide(
                ControlType.BackupRecovery,
                input.BackupMaturity.ToString(),
                input.BackupMaturity == BackupMaturity.Mature,
                "Mature backup and recovery is claimed and receives material rating credit.",
                new
                {
                    input.ControlDetails?.BackupsImmutableOrOffline,
                    input.ControlDetails?.BackupCredentialsSeparated,
                    input.ControlDetails?.RestoreTestedLast12Months,
                    input.ControlDetails?.RecoveryPointObjectiveHours,
                    input.ControlDetails?.RecoveryTimeObjectiveHours
                }),
            Decide(
                ControlType.IncidentResponsePlan,
                input.HasIncidentResponsePlan ? "InPlace" : "NotInPlace",
                input.HasIncidentResponsePlan,
                "An incident response plan is claimed in place and receives rating credit.",
                new
                {
                    input.ControlDetails?.IncidentPlanApproved,
                    input.ControlDetails?.IncidentPlanUpdatedLast12Months,
                    input.ControlDetails?.IncidentPlanTestedLast12Months,
                    input.ControlDetails?.IncidentRolesNamed
                }),
            Decide(
                ControlType.SensitiveData,
                input.SensitiveDataExposure.ToString(),
                input.SensitiveDataExposure == SensitiveDataExposure.Low && (materialLimit || priorLoss),
                "Low sensitive-data exposure is claimed for a materially exposed risk.",
                new
                {
                    input.ControlDetails?.SensitiveDataInventoryMaintained,
                    input.ControlDetails?.SensitiveDataEncrypted,
                    input.ControlDetails?.SensitiveDataTypes,
                    input.ControlDetails?.SensitiveDataVolume
                })
        ];
    }

    public static IReadOnlyList<ControlAssertionDecision> ApplyReassessmentRules(
        IReadOnlyCollection<ControlAssertionDecision> current,
        IReadOnlyCollection<ControlAssertion> previous)
    {
        var previousByType = previous.ToDictionary(assertion => assertion.ControlType);
        var changed = current
            .Where(decision => previousByType.TryGetValue(decision.ControlType, out var prior)
                && !string.Equals(prior.ClaimedState, decision.ClaimedState, StringComparison.Ordinal))
            .ToArray();

        if (changed.Length == 0)
            throw new BusinessConflictException(
                "quote.reassessment.no_changes",
                "Change at least one control answer before creating a reassessment.");

        return current
            .Select(decision =>
            {
                if (!previousByType.TryGetValue(decision.ControlType, out var prior)
                    || !IsImprovement(decision.ControlType, prior.ClaimedState, decision.ClaimedState))
                {
                    return decision;
                }

                return decision with
                {
                    EvidenceRequired = true,
                    EvidenceReason = "This reassessment claims an improved control. Supporting evidence is required before acceptance."
                };
            })
            .ToArray();
    }

    private static bool IsImprovement(ControlType controlType, string previous, string current)
    {
        return Rank(controlType, current) > Rank(controlType, previous);
    }

    private static int Rank(ControlType controlType, string value) => controlType switch
    {
        ControlType.MultiFactorAuthentication or ControlType.EndpointDetectionAndResponse => value switch
        {
            "Implemented" => 3,
            "Partial" => 2,
            _ => 1
        },
        ControlType.BackupRecovery => value switch
        {
            "Mature" => 3,
            "Partial" => 2,
            _ => 1
        },
        ControlType.IncidentResponsePlan => value == "InPlace" ? 2 : 1,
        ControlType.SensitiveData => value switch
        {
            "Low" => 4,
            "Moderate" => 3,
            "High" => 2,
            _ => 1
        },
        _ => 0
    };

    private static ControlAssertionDecision Decide(
        ControlType controlType,
        string claimedState,
        bool evidenceRequired,
        string reason,
        object details)
    {
        return new ControlAssertionDecision(
            controlType,
            claimedState,
            evidenceRequired,
            evidenceRequired ? reason : string.Empty,
            JsonSerializer.Serialize(details, JsonSerializerOptions.Web));
    }
}

public sealed record CreateQuoteAssuranceInput(
    decimal RequestedLimit,
    CyberSecurityControlStatus MfaStatus,
    CyberSecurityControlStatus EdrStatus,
    BackupMaturity BackupMaturity,
    bool HasIncidentResponsePlan,
    int PriorCyberIncidents,
    SensitiveDataExposure SensitiveDataExposure,
    CyberControlDetails? ControlDetails = null);
