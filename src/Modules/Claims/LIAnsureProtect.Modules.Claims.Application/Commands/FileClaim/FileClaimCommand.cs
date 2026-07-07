using LIAnsureProtect.Modules.Claims.Domain;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Claims.Application.Commands.FileClaim;

/// <summary>
/// FNOL — the claimant files a cyber claim against one of their bound policies. Returns null when
/// the policy does not exist or is not owned by the caller (the API maps null to 404 so existence
/// is never leaked); business rejections (not bound, incident outside the policy period) throw
/// <see cref="InvalidOperationException"/> (mapped to 409).
/// </summary>
public sealed record FileClaimCommand(
    Guid PolicyId,
    ClaimIncidentType IncidentType,
    DateTime IncidentAtUtc,
    DateTime DiscoveredAtUtc,
    string Description) : IRequest<ClaimResult?>;

public sealed class FileClaimCommandHandler(
    IClaimRepository claims,
    IClaimsPolicyContextReader policyContextReader,
    ICurrentUser currentUser)
    : IRequestHandler<FileClaimCommand, ClaimResult?>
{
    public async Task<ClaimResult?> Handle(FileClaimCommand request, CancellationToken cancellationToken)
    {
        var claimantUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to file a claim.")
            : currentUser.UserId;

        var policy = await policyContextReader.GetForClaimFilingAsync(request.PolicyId, cancellationToken);
        if (policy is null)
            return null;

        // Ownership before any business detail: an unowned policy looks exactly like a missing one.
        if (!string.Equals(policy.OwnerUserId, claimantUserId, StringComparison.Ordinal))
            return null;

        if (!string.Equals(policy.Status, "Bound", StringComparison.Ordinal))
            throw new InvalidOperationException("Claims can only be filed against a bound policy.");

        var filedAtUtc = DateTime.UtcNow;
        var claim = Claim.File(
            policy.PolicyId,
            policy.SubmissionId,
            claimantUserId,
            ClaimNumberGenerator.Create(filedAtUtc),
            request.IncidentType,
            request.IncidentAtUtc,
            request.DiscoveredAtUtc,
            request.Description,
            policy.PolicyNumber,
            policy.EffectiveAtUtc,
            policy.ExpirationAtUtc,
            policy.Limit,
            policy.Retention,
            filedAtUtc);

        await claims.AddAsync(claim, cancellationToken);
        await claims.SaveChangesAsync(cancellationToken);

        return ClaimResultFactory.FromClaim(claim);
    }
}
