using LIAnsureProtect.Modules.Claims.Domain;

namespace LIAnsureProtect.Modules.Claims.Application;

/// <summary>Working-state summary returned by adjudication actions and the queue.</summary>
public sealed record ClaimAdjudicationResult(
    Guid ClaimId,
    string ClaimNumber,
    Guid PolicyId,
    string PolicyNumber,
    string IncidentType,
    DateTime IncidentAtUtc,
    string Status,
    string? AssignedAdjusterUserId,
    int OpenInformationRequestCount,
    DateTime FiledAtUtc,
    DateTime UpdatedAtUtc);

public sealed record ClaimWorkNoteResult(
    Guid NoteId,
    Guid ClaimId,
    string Note,
    string CreatedByUserId,
    DateTime CreatedAtUtc);

public sealed record ClaimInformationRequestResult(
    Guid InformationRequestId,
    Guid ClaimId,
    string Title,
    string Message,
    string RequestedByUserId,
    DateTime RequestedAtUtc,
    bool IsAnswered,
    string? ResponseText,
    string? RespondedByUserId,
    DateTime? RespondedAtUtc);

/// <summary>Full working detail for the adjuster's file view.</summary>
public sealed record ClaimAdjudicationDetailResult(
    Guid ClaimId,
    string ClaimNumber,
    Guid PolicyId,
    string PolicyNumber,
    string OwnerUserId,
    string IncidentType,
    DateTime IncidentAtUtc,
    DateTime DiscoveredAtUtc,
    string Description,
    string Status,
    string? AssignedAdjusterUserId,
    decimal? ClaimedAmount,
    decimal ReserveAmount,
    decimal PaidAmount,
    decimal? SettlementAmount,
    string? DenialReason,
    string? DenialNarrative,
    DateTime? DecidedAtUtc,
    DateTime? ClosedAtUtc,
    decimal PolicyLimitAtFiling,
    decimal PolicyRetentionAtFiling,
    DateTime PolicyEffectiveAtFiling,
    DateTime PolicyExpirationAtFiling,
    DateTime FiledAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyCollection<Commands.ManageClaimFinancials.ClaimReserveChangeResult> ReserveHistory,
    IReadOnlyCollection<Commands.DecideClaim.ClaimDecisionResult> Decisions,
    IReadOnlyCollection<ClaimWorkNoteResult> WorkNotes,
    IReadOnlyCollection<ClaimInformationRequestResult> InformationRequests,
    IReadOnlyCollection<Documents.ClaimDocumentResult> Documents,
    IReadOnlyCollection<ClaimTimelineEntryResult> Timeline);

public static class ClaimAdjudicationResultFactory
{
    public static ClaimAdjudicationResult FromClaim(Claim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        return new ClaimAdjudicationResult(
            claim.Id,
            claim.ClaimNumber,
            claim.PolicyId,
            claim.PolicyNumberAtFiling,
            claim.IncidentType.ToString(),
            claim.IncidentAtUtc,
            claim.Status.ToString(),
            claim.AssignedAdjusterUserId,
            claim.InformationRequests.Count(request => !request.IsAnswered),
            claim.FiledAtUtc,
            claim.UpdatedAtUtc);
    }

    public static ClaimInformationRequestResult FromInformationRequest(ClaimInformationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ClaimInformationRequestResult(
            request.Id,
            request.ClaimId,
            request.Title,
            request.Message,
            request.RequestedByUserId,
            request.RequestedAtUtc,
            request.IsAnswered,
            request.ResponseText,
            request.RespondedByUserId,
            request.RespondedAtUtc);
    }

    public static ClaimWorkNoteResult FromWorkNote(ClaimWorkNote note)
    {
        ArgumentNullException.ThrowIfNull(note);

        return new ClaimWorkNoteResult(
            note.Id,
            note.ClaimId,
            note.Note,
            note.CreatedByUserId,
            note.CreatedAtUtc);
    }
}
