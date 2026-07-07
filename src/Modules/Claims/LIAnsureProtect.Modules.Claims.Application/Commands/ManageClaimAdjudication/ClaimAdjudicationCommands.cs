using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Claims.Application.Commands.ManageClaimAdjudication;

public sealed record AssignClaimToMeCommand(Guid ClaimId) : IRequest<ClaimAdjudicationResult?>;

public sealed record ReleaseClaimAssignmentCommand(Guid ClaimId) : IRequest<ClaimAdjudicationResult?>;

public sealed record AddClaimWorkNoteCommand(Guid ClaimId, string Note) : IRequest<ClaimWorkNoteResult?>;

public sealed record RequestClaimInformationCommand(
    Guid ClaimId,
    string Title,
    string Message) : IRequest<ClaimInformationRequestResult?>;

public sealed class AssignClaimToMeCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser)
    : IRequestHandler<AssignClaimToMeCommand, ClaimAdjudicationResult?>
{
    public async Task<ClaimAdjudicationResult?> Handle(
        AssignClaimToMeCommand request,
        CancellationToken cancellationToken)
    {
        var claim = await claims.GetByIdForUpdateAsync(request.ClaimId, cancellationToken);
        if (claim is null)
            return null;

        claim.AssignTo(CurrentAdjusterUser.GetRequiredUserId(currentUser), DateTime.UtcNow);
        await claims.SaveChangesAsync(cancellationToken);

        return ClaimAdjudicationResultFactory.FromClaim(claim);
    }
}

public sealed class ReleaseClaimAssignmentCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser)
    : IRequestHandler<ReleaseClaimAssignmentCommand, ClaimAdjudicationResult?>
{
    public async Task<ClaimAdjudicationResult?> Handle(
        ReleaseClaimAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        var claim = await claims.GetByIdForUpdateAsync(request.ClaimId, cancellationToken);
        if (claim is null)
            return null;

        claim.ReleaseAssignment(CurrentAdjusterUser.GetRequiredUserId(currentUser), DateTime.UtcNow);
        await claims.SaveChangesAsync(cancellationToken);

        return ClaimAdjudicationResultFactory.FromClaim(claim);
    }
}

public sealed class AddClaimWorkNoteCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser)
    : IRequestHandler<AddClaimWorkNoteCommand, ClaimWorkNoteResult?>
{
    public async Task<ClaimWorkNoteResult?> Handle(
        AddClaimWorkNoteCommand request,
        CancellationToken cancellationToken)
    {
        var claim = await claims.GetByIdForUpdateAsync(request.ClaimId, cancellationToken);
        if (claim is null)
            return null;

        var note = claim.AddWorkNote(
            CurrentAdjusterUser.GetRequiredUserId(currentUser),
            request.Note,
            DateTime.UtcNow);
        await claims.SaveChangesAsync(cancellationToken);

        return ClaimAdjudicationResultFactory.FromWorkNote(note);
    }
}

public sealed class RequestClaimInformationCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser)
    : IRequestHandler<RequestClaimInformationCommand, ClaimInformationRequestResult?>
{
    public async Task<ClaimInformationRequestResult?> Handle(
        RequestClaimInformationCommand request,
        CancellationToken cancellationToken)
    {
        var claim = await claims.GetByIdForUpdateAsync(request.ClaimId, cancellationToken);
        if (claim is null)
            return null;

        var informationRequest = claim.RequestInformation(
            CurrentAdjusterUser.GetRequiredUserId(currentUser),
            request.Title,
            request.Message,
            DateTime.UtcNow);
        await claims.SaveChangesAsync(cancellationToken);

        return ClaimAdjudicationResultFactory.FromInformationRequest(informationRequest);
    }
}

internal static class CurrentAdjusterUser
{
    public static string GetRequiredUserId(ICurrentUser currentUser)
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated adjuster user id is required to work claims.")
            : currentUser.UserId;
    }
}
