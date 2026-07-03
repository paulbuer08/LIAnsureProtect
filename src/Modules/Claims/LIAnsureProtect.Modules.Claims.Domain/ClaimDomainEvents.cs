using LIAnsureProtect.Platform.Abstractions.DomainEvents;

namespace LIAnsureProtect.Modules.Claims.Domain;

/// <summary>Raised when a claimant files a new claim (FNOL) against a bound policy.</summary>
public sealed record ClaimFiledDomainEvent(
    Guid ClaimId,
    string ClaimNumber,
    Guid PolicyId,
    string PolicyNumber,
    string OwnerUserId,
    ClaimIncidentType IncidentType,
    DateTime OccurredAtUtc) : IDomainEvent;
