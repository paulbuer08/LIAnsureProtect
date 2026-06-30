namespace LIAnsureProtect.Modules.Underwriting.Application.Referrals;

/// <summary>
/// Inbound read port the legacy referral-queue and timeline reads call to fetch the operation side of
/// the combined view. The module owns the operation data; the quote/evidence/decision-audit sides stay
/// legacy. Reads are no-tracking.
/// </summary>
public interface IReferralOperationsReader
{
    Task<IReadOnlyCollection<ReferralOperationSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> quoteIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ReferralOperationTimelineItem>?> GetTimelineAsync(
        Guid quoteId,
        CancellationToken cancellationToken);
}

public sealed record ReferralOperationSummary(
    Guid QuoteId,
    string? AssignedUnderwriterUserId,
    string Priority,
    DateTime DueAtUtc,
    bool IsSlaBreached,
    string Status,
    int OpenTaskCount,
    DateTime? LatestTimelineAtUtc);

public sealed record ReferralOperationTimelineItem(
    string EntryType,
    string Summary,
    string CreatedByUserId,
    DateTime CreatedAtUtc);
