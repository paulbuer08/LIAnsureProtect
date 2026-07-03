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

/// <summary>Raised when an adjuster claims the file (assignment changed to a new adjuster).</summary>
public sealed record ClaimAssignedDomainEvent(
    Guid ClaimId,
    string ClaimNumber,
    Guid PolicyId,
    string OwnerUserId,
    string AdjusterUserId,
    DateTime OccurredAtUtc) : IDomainEvent;

/// <summary>Raised when an adjuster asks the claimant for more information.</summary>
public sealed record ClaimInformationRequestedDomainEvent(
    Guid ClaimId,
    string ClaimNumber,
    Guid InformationRequestId,
    Guid PolicyId,
    string OwnerUserId,
    string RequestedByUserId,
    string Title,
    DateTime OccurredAtUtc) : IDomainEvent;

/// <summary>Raised for each supporting document the claimant uploads (after its quarantine scan).</summary>
public sealed record ClaimDocumentUploadedDomainEvent(
    Guid ClaimId,
    string ClaimNumber,
    Guid DocumentId,
    Guid PolicyId,
    string OwnerUserId,
    ClaimDocumentKind Kind,
    string OriginalFileName,
    string? AssignedAdjusterUserId,
    DateTime OccurredAtUtc) : IDomainEvent;

/// <summary>Raised when the assigned adjuster accepts the claim with a settlement.</summary>
public sealed record ClaimAcceptedDomainEvent(
    Guid ClaimId,
    string ClaimNumber,
    Guid PolicyId,
    string OwnerUserId,
    string DecidedByUserId,
    decimal SettlementAmount,
    DateTime OccurredAtUtc) : IDomainEvent;

/// <summary>Raised when the assigned adjuster denies the claim.</summary>
public sealed record ClaimDeniedDomainEvent(
    Guid ClaimId,
    string ClaimNumber,
    Guid PolicyId,
    string OwnerUserId,
    string DecidedByUserId,
    ClaimDenialReason DenialReason,
    DateTime OccurredAtUtc) : IDomainEvent;

/// <summary>Raised when a decided claim is closed.</summary>
public sealed record ClaimClosedDomainEvent(
    Guid ClaimId,
    string ClaimNumber,
    Guid PolicyId,
    string OwnerUserId,
    string ClosedByUserId,
    ClaimStatus OutcomeAtClose,
    DateTime OccurredAtUtc) : IDomainEvent;

/// <summary>Raised when the claimant answers an information request.</summary>
public sealed record ClaimantInformationResponseDomainEvent(
    Guid ClaimId,
    string ClaimNumber,
    Guid InformationRequestId,
    Guid PolicyId,
    string OwnerUserId,
    string RespondedByUserId,
    string? AssignedAdjusterUserId,
    DateTime OccurredAtUtc) : IDomainEvent;
