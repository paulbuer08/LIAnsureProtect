namespace LIAnsureProtect.Modules.Claims.Domain;

/// <summary>Why a claim was denied (the category; the narrative carries the specifics).</summary>
public enum ClaimDenialReason
{
    NotCovered,
    PolicyExclusion,
    OutsidePolicyPeriod,
    InsufficientEvidence,
    MisrepresentationFraud,
    Other
}
