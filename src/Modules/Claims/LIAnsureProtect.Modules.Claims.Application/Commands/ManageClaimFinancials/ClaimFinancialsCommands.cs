using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Claims.Application.Commands.ManageClaimFinancials;

/// <summary>
/// The claimant declares (or updates) the claimed amount. Owner-scoped: a claim that is missing
/// or owned by someone else returns null (→ 404).
/// </summary>
public sealed record SetClaimedAmountCommand(Guid ClaimId, decimal Amount) : IRequest<ClaimFinancialsResult?>;

/// <summary>
/// The assigned adjuster sets or adjusts the reserve with a mandatory reason; every change
/// appends an audit row. The domain enforces assigned-adjuster-only (→ 409 for anyone else).
/// </summary>
public sealed record SetClaimReserveCommand(Guid ClaimId, decimal Amount, string Reason) : IRequest<ClaimFinancialsResult?>;

/// <summary>The claim's money picture after a financial action.</summary>
public sealed record ClaimFinancialsResult(
    Guid ClaimId,
    decimal? ClaimedAmount,
    decimal ReserveAmount,
    decimal PaidAmount,
    decimal PolicyLimitAtFiling,
    decimal PolicyRetentionAtFiling);

public sealed record ClaimReserveChangeResult(
    Guid ChangeId,
    decimal OldAmount,
    decimal NewAmount,
    string Reason,
    string ChangedByUserId,
    DateTime ChangedAtUtc);

public sealed class SetClaimedAmountCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser)
    : IRequestHandler<SetClaimedAmountCommand, ClaimFinancialsResult?>
{
    public async Task<ClaimFinancialsResult?> Handle(
        SetClaimedAmountCommand request,
        CancellationToken cancellationToken)
    {
        var claimantUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to declare a claimed amount.")
            : currentUser.UserId;

        var claim = await claims.GetByIdForUpdateAsync(request.ClaimId, cancellationToken);
        if (claim is null || !string.Equals(claim.OwnerUserId, claimantUserId, StringComparison.Ordinal))
            return null;

        claim.SetClaimedAmount(request.Amount, claimantUserId, DateTime.UtcNow);
        await claims.SaveChangesAsync(cancellationToken);

        return ClaimFinancialsResultFactory.FromClaim(claim);
    }
}

public sealed class SetClaimReserveCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser)
    : IRequestHandler<SetClaimReserveCommand, ClaimFinancialsResult?>
{
    public async Task<ClaimFinancialsResult?> Handle(
        SetClaimReserveCommand request,
        CancellationToken cancellationToken)
    {
        var adjusterUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated adjuster user id is required to set a reserve.")
            : currentUser.UserId;

        var claim = await claims.GetByIdForUpdateAsync(request.ClaimId, cancellationToken);
        if (claim is null)
            return null;

        claim.SetReserve(request.Amount, request.Reason, adjusterUserId, DateTime.UtcNow);
        await claims.SaveChangesAsync(cancellationToken);

        return ClaimFinancialsResultFactory.FromClaim(claim);
    }
}

public static class ClaimFinancialsResultFactory
{
    public static ClaimFinancialsResult FromClaim(Domain.Claim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        return new ClaimFinancialsResult(
            claim.Id,
            claim.ClaimedAmount,
            claim.ReserveAmount,
            claim.PaidAmount,
            claim.PolicyLimitAtFiling,
            claim.PolicyRetentionAtFiling);
    }

    public static ClaimReserveChangeResult FromReserveChange(Domain.ClaimReserveChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        return new ClaimReserveChangeResult(
            change.Id,
            change.OldAmount,
            change.NewAmount,
            change.Reason,
            change.ChangedByUserId,
            change.ChangedAtUtc);
    }
}
