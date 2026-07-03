using LIAnsureProtect.Modules.Claims.Domain;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Claims.Application.Commands.DecideClaim;

/// <summary>
/// The assigned adjuster accepts the claim with a settlement (capped at the file-time policy
/// limit net of retention — the domain enforces every guardrail).
/// </summary>
public sealed record AcceptClaimCommand(
    Guid ClaimId,
    decimal SettlementAmount,
    string Reason,
    string? Notes) : IRequest<ClaimDecisionResult?>;

/// <summary>The assigned adjuster denies the claim with a reason category and narrative.</summary>
public sealed record DenyClaimCommand(
    Guid ClaimId,
    ClaimDenialReason DenialReason,
    string Narrative) : IRequest<ClaimDecisionResult?>;

/// <summary>The assigned adjuster closes a decided claim.</summary>
public sealed record CloseClaimCommand(Guid ClaimId) : IRequest<ClaimDecisionResult?>;

/// <summary>The verdict as returned by decision actions and listed in the audit history.</summary>
public sealed record ClaimDecisionResult(
    Guid ClaimId,
    string ClaimNumber,
    string Status,
    string Outcome,
    decimal? SettlementAmount,
    decimal PaidAmount,
    string? DenialReason,
    string Reason,
    string? Notes,
    decimal? ClaimedAmountAtDecision,
    decimal ReserveAmountAtDecision,
    string DecidedByUserId,
    DateTime DecidedAtUtc);

public sealed class AcceptClaimCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser)
    : IRequestHandler<AcceptClaimCommand, ClaimDecisionResult?>
{
    public async Task<ClaimDecisionResult?> Handle(AcceptClaimCommand request, CancellationToken cancellationToken)
    {
        var adjusterUserId = ClaimDecisionUser.GetRequiredUserId(currentUser);
        var claim = await claims.GetByIdForUpdateAsync(request.ClaimId, cancellationToken);
        if (claim is null)
            return null;

        claim.Accept(request.SettlementAmount, request.Reason, request.Notes, adjusterUserId, DateTime.UtcNow);
        await claims.SaveChangesAsync(cancellationToken);

        return ClaimDecisionResultFactory.FromLatestDecision(claim);
    }
}

public sealed class DenyClaimCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser)
    : IRequestHandler<DenyClaimCommand, ClaimDecisionResult?>
{
    public async Task<ClaimDecisionResult?> Handle(DenyClaimCommand request, CancellationToken cancellationToken)
    {
        var adjusterUserId = ClaimDecisionUser.GetRequiredUserId(currentUser);
        var claim = await claims.GetByIdForUpdateAsync(request.ClaimId, cancellationToken);
        if (claim is null)
            return null;

        claim.Deny(request.DenialReason, request.Narrative, adjusterUserId, DateTime.UtcNow);
        await claims.SaveChangesAsync(cancellationToken);

        return ClaimDecisionResultFactory.FromLatestDecision(claim);
    }
}

public sealed class CloseClaimCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser)
    : IRequestHandler<CloseClaimCommand, ClaimDecisionResult?>
{
    public async Task<ClaimDecisionResult?> Handle(CloseClaimCommand request, CancellationToken cancellationToken)
    {
        var adjusterUserId = ClaimDecisionUser.GetRequiredUserId(currentUser);
        var claim = await claims.GetByIdForUpdateAsync(request.ClaimId, cancellationToken);
        if (claim is null)
            return null;

        claim.Close(adjusterUserId, DateTime.UtcNow);
        await claims.SaveChangesAsync(cancellationToken);

        return ClaimDecisionResultFactory.FromLatestDecision(claim);
    }
}

public static class ClaimDecisionResultFactory
{
    public static ClaimDecisionResult FromLatestDecision(Claim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var decision = claim.Decisions.OrderBy(candidate => candidate.DecidedAtUtc).Last();

        return FromDecision(claim, decision);
    }

    public static ClaimDecisionResult FromDecision(Claim claim, ClaimDecision decision)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(decision);

        return new ClaimDecisionResult(
            claim.Id,
            claim.ClaimNumber,
            claim.Status.ToString(),
            decision.Outcome.ToString(),
            decision.SettlementAmount,
            claim.PaidAmount,
            decision.DenialReason?.ToString(),
            decision.Reason,
            decision.Notes,
            decision.ClaimedAmountAtDecision,
            decision.ReserveAmountAtDecision,
            decision.DecidedByUserId,
            decision.DecidedAtUtc);
    }
}

internal static class ClaimDecisionUser
{
    public static string GetRequiredUserId(ICurrentUser currentUser)
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated adjuster user id is required to decide claims.")
            : currentUser.UserId;
    }
}
