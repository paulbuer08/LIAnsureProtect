namespace LIAnsureProtect.Modules.Claims.Domain;

/// <summary>
/// The claim lifecycle. Transitions are enforced by the <see cref="Claim"/> aggregate's guard
/// methods — an illegal transition throws instead of corrupting state:
/// Filed → UnderReview → InformationRequested → (back to UnderReview) → Accepted/Denied → Closed.
/// </summary>
public enum ClaimStatus
{
    Filed,
    UnderReview,
    InformationRequested,
    Accepted,
    Denied,
    Closed
}
