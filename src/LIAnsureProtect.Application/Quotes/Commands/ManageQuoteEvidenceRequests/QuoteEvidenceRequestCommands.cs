using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Application.Common.Security;
using LIAnsureProtect.Domain.Quotes;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.ManageQuoteEvidenceRequests;

public sealed record CreateQuoteEvidenceRequestCommand(
    Guid QuoteId,
    EvidenceRequestCategory Category,
    string Title,
    string Description,
    DateTime DueAtUtc) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record RespondToQuoteEvidenceRequestCommand(
    Guid EvidenceRequestId,
    string RespondentName,
    string RespondentTitle,
    string ResponseText,
    string? AttachmentFileName,
    string? AttachmentContentType,
    long? AttachmentSizeBytes) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record AcceptQuoteEvidenceRequestCommand(
    Guid QuoteId,
    Guid EvidenceRequestId,
    string? ReviewNotes) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record CancelQuoteEvidenceRequestCommand(
    Guid QuoteId,
    Guid EvidenceRequestId,
    string? ReviewNotes) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record FollowUpQuoteEvidenceRequestCommand(
    Guid QuoteId,
    Guid EvidenceRequestId) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record ListOwnerEvidenceRequestsQuery : IRequest<ListOwnerEvidenceRequestsResult>;

public sealed record ListOwnerEvidenceRequestsResult(
    IReadOnlyCollection<QuoteEvidenceRequestResult> EvidenceRequests);

public sealed record QuoteEvidenceRequestResult(
    Guid EvidenceRequestId,
    Guid QuoteId,
    Guid SubmissionId,
    string Category,
    string Title,
    string Description,
    DateTime DueAtUtc,
    string Status,
    bool IsOverdue,
    int DaysUntilDue,
    string RequestedByUserId,
    DateTime RequestedAtUtc,
    string? RespondedByUserId,
    string? RespondentName,
    string? RespondentTitle,
    string? ResponseText,
    string? AttachmentFileName,
    string? AttachmentContentType,
    long? AttachmentSizeBytes,
    DateTime? RespondedAtUtc,
    string? AcceptedByUserId,
    DateTime? AcceptedAtUtc,
    string? CancelledByUserId,
    DateTime? CancelledAtUtc,
    string? ReviewNotes,
    DateTime UpdatedAtUtc);

public sealed class CreateQuoteEvidenceRequestCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<CreateQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        CreateQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var quote = await quoteRepository.GetForUnderwritingReviewAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return null;

        if (quote.Status != QuoteStatus.Referred)
            throw new InvalidOperationException("Evidence can only be requested while a quote is referred.");

        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(request.QuoteId, cancellationToken)
            ?? throw new InvalidOperationException("Referral operations must exist before evidence can be requested.");

        var requestedAtUtc = DateTime.UtcNow;
        var evidenceRequest = QuoteEvidenceRequest.Create(
            quote.Id,
            quote.SubmissionId,
            operation.Id,
            quote.OwnerUserId,
            CurrentUserId.GetRequired(currentUser, "An authenticated underwriter user id is required to request evidence."),
            request.Category,
            request.Title,
            request.Description,
            request.DueAtUtc,
            requestedAtUtc);

        await quoteRepository.AddEvidenceRequestAsync(evidenceRequest, cancellationToken);
        operation.RecordEvidenceRequestCreated(
            evidenceRequest.Id,
            evidenceRequest.RequestedByUserId,
            requestedAtUtc);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest);
    }
}

public sealed class RespondToQuoteEvidenceRequestCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<RespondToQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        RespondToQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated owner user id is required to respond to evidence requests.");
        var evidenceRequest = await quoteRepository.GetEvidenceRequestForOwnerAsync(
            request.EvidenceRequestId,
            ownerUserId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(
            evidenceRequest.QuoteId,
            cancellationToken)
            ?? throw new InvalidOperationException("Referral operations must exist before evidence can be updated.");
        var respondedAtUtc = DateTime.UtcNow;

        evidenceRequest.Respond(
            ownerUserId,
            request.RespondentName,
            request.RespondentTitle,
            request.ResponseText,
            request.AttachmentFileName,
            request.AttachmentContentType,
            request.AttachmentSizeBytes,
            respondedAtUtc);
        operation.RecordEvidenceRequestResponded(
            evidenceRequest.Id,
            ownerUserId,
            respondedAtUtc);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest);
    }
}

public sealed class AcceptQuoteEvidenceRequestCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<AcceptQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        AcceptQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await quoteRepository.GetEvidenceRequestForUnderwritingAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(
            request.QuoteId,
            cancellationToken)
            ?? throw new InvalidOperationException("Referral operations must exist before evidence can be reviewed.");
        var underwriterUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated underwriter user id is required to accept evidence.");
        var acceptedAtUtc = DateTime.UtcNow;

        evidenceRequest.Accept(underwriterUserId, request.ReviewNotes, acceptedAtUtc);
        operation.RecordEvidenceRequestAccepted(evidenceRequest.Id, underwriterUserId, acceptedAtUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest);
    }
}

public sealed class CancelQuoteEvidenceRequestCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<CancelQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        CancelQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await quoteRepository.GetEvidenceRequestForUnderwritingAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(
            request.QuoteId,
            cancellationToken)
            ?? throw new InvalidOperationException("Referral operations must exist before evidence can be reviewed.");
        var underwriterUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated underwriter user id is required to cancel evidence.");
        var cancelledAtUtc = DateTime.UtcNow;

        evidenceRequest.Cancel(underwriterUserId, request.ReviewNotes, cancelledAtUtc);
        operation.RecordEvidenceRequestCancelled(evidenceRequest.Id, underwriterUserId, cancelledAtUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest);
    }
}

public sealed class FollowUpQuoteEvidenceRequestCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<FollowUpQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        FollowUpQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await quoteRepository.GetEvidenceRequestForUnderwritingAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var operation = await quoteRepository.GetReferralOperationForUpdateAsync(
            request.QuoteId,
            cancellationToken)
            ?? throw new InvalidOperationException("Referral operations must exist before evidence can receive follow-up.");
        var underwriterUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated underwriter user id is required to follow up evidence.");
        var followedUpAtUtc = DateTime.UtcNow;

        evidenceRequest.RecordFollowUpSent(underwriterUserId, followedUpAtUtc);
        operation.RecordEvidenceRequestFollowUpSent(evidenceRequest.Id, underwriterUserId, followedUpAtUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest);
    }
}

public sealed class ListOwnerEvidenceRequestsQueryHandler(
    IQuoteRepository quoteRepository,
    ICurrentUser currentUser)
    : IRequestHandler<ListOwnerEvidenceRequestsQuery, ListOwnerEvidenceRequestsResult>
{
    public async Task<ListOwnerEvidenceRequestsResult> Handle(
        ListOwnerEvidenceRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = CurrentUserId.GetRequired(
            currentUser,
            "An authenticated owner user id is required to list evidence requests.");
        var evidenceRequests = await quoteRepository.ListEvidenceRequestsForOwnerAsync(
            ownerUserId,
            cancellationToken);

        return new ListOwnerEvidenceRequestsResult(
            evidenceRequests
                .Select(QuoteEvidenceRequestResultFactory.FromRequest)
                .ToList());
    }
}

public static class QuoteEvidenceRequestResultFactory
{
    public static QuoteEvidenceRequestResult FromRequest(QuoteEvidenceRequest request)
    {
        return new QuoteEvidenceRequestResult(
            request.Id,
            request.QuoteId,
            request.SubmissionId,
            request.Category.ToString(),
            request.Title,
            request.Description,
            request.DueAtUtc,
            request.Status.ToString(),
            request.Status == EvidenceRequestStatus.Open && request.DueAtUtc < DateTime.UtcNow,
            (request.DueAtUtc.Date - DateTime.UtcNow.Date).Days,
            request.RequestedByUserId,
            request.RequestedAtUtc,
            request.RespondedByUserId,
            request.RespondentName,
            request.RespondentTitle,
            request.ResponseText,
            request.AttachmentFileName,
            request.AttachmentContentType,
            request.AttachmentSizeBytes,
            request.RespondedAtUtc,
            request.AcceptedByUserId,
            request.AcceptedAtUtc,
            request.CancelledByUserId,
            request.CancelledAtUtc,
            request.ReviewNotes,
            request.UpdatedAtUtc);
    }
}

internal static class CurrentUserId
{
    public static string GetRequired(ICurrentUser currentUser, string message)
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException(message)
            : currentUser.UserId;
    }
}
