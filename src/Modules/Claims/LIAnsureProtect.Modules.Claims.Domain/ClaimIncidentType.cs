namespace LIAnsureProtect.Modules.Claims.Domain;

/// <summary>The cyber incident categories a claim can be filed for (Cyber is the only product line).</summary>
public enum ClaimIncidentType
{
    RansomwareExtortion,
    BusinessEmailCompromise,
    DataBreachPrivacy,
    NetworkInterruption,
    FundsTransferFraud,
    Other
}
