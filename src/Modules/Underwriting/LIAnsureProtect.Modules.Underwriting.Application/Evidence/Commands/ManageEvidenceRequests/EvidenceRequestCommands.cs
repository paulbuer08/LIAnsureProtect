using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Commands.ManageEvidenceRequests;

public sealed record CreateQuoteEvidenceRequestCommand(
    Guid QuoteId,
    EvidenceRequestCategory Category,
    string Title,
    string Description,
    DateTime DueAtUtc) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record CancelQuoteEvidenceRequestCommand(
    Guid QuoteId,
    Guid EvidenceRequestId,
    string? ReviewNotes) : IRequest<QuoteEvidenceRequestResult?>;

public sealed record FollowUpQuoteEvidenceRequestCommand(
    Guid QuoteId,
    Guid EvidenceRequestId) : IRequest<QuoteEvidenceRequestResult?>;

public sealed class CreateQuoteEvidenceRequestCommandHandler(
    IEvidenceRequestRepository evidence,
    IUnderwritingQuoteContextReader quoteContextReader,
    ICurrentUser currentUser)
    : IRequestHandler<CreateQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        CreateQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var quote = await quoteContextReader.GetForAiReviewAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return null;

        if (!string.Equals(quote.Status, "Referred", StringComparison.Ordinal))
            throw new InvalidOperationException("Evidence can only be requested while a quote is referred.");

        var requestedAtUtc = DateTime.UtcNow;
        var evidenceRequest = QuoteEvidenceRequest.Create(
            quote.QuoteId,
            quote.SubmissionId,
            quote.OwnerUserId,
            CurrentEvidenceUser.GetRequiredUserId(
                currentUser,
                "An authenticated underwriter user id is required to request evidence."),
            request.Category,
            request.Title,
            request.Description,
            request.DueAtUtc,
            requestedAtUtc,
            quote.Version);

        await evidence.AddAsync(evidenceRequest, cancellationToken);
        await evidence.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest);
    }
}

public sealed class CancelQuoteEvidenceRequestCommandHandler(
    IEvidenceRequestRepository evidence,
    ICurrentUser currentUser)
    : IRequestHandler<CancelQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        CancelQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await evidence.GetForUnderwritingAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var underwriterUserId = CurrentEvidenceUser.GetRequiredUserId(
            currentUser,
            "An authenticated underwriter user id is required to cancel evidence.");
        var cancelledAtUtc = DateTime.UtcNow;

        evidenceRequest.Cancel(underwriterUserId, request.ReviewNotes, cancelledAtUtc);
        await evidence.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest);
    }
}

public sealed class FollowUpQuoteEvidenceRequestCommandHandler(
    IEvidenceRequestRepository evidence,
    ICurrentUser currentUser)
    : IRequestHandler<FollowUpQuoteEvidenceRequestCommand, QuoteEvidenceRequestResult?>
{
    public async Task<QuoteEvidenceRequestResult?> Handle(
        FollowUpQuoteEvidenceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var evidenceRequest = await evidence.GetForUnderwritingAsync(
            request.QuoteId,
            request.EvidenceRequestId,
            cancellationToken);
        if (evidenceRequest is null)
            return null;

        var underwriterUserId = CurrentEvidenceUser.GetRequiredUserId(
            currentUser,
            "An authenticated underwriter user id is required to follow up evidence.");
        var followedUpAtUtc = DateTime.UtcNow;

        evidenceRequest.RecordFollowUpSent(underwriterUserId, followedUpAtUtc);
        await evidence.SaveChangesAsync(cancellationToken);

        return QuoteEvidenceRequestResultFactory.FromRequest(evidenceRequest);
    }
}
