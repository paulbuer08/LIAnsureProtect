using LIAnsureProtect.Application.Common.Persistence;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.AcceptQuote;

public sealed class AcceptQuoteCommandHandler(
    IQuoteRepository quoteRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<AcceptQuoteCommand, AcceptQuoteResult?>
{
    public async Task<AcceptQuoteResult?> Handle(
        AcceptQuoteCommand request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = GetRequiredCurrentUserId();
        var quote = await quoteRepository.GetOwnedForAcceptanceAsync(
            request.QuoteId,
            ownerUserId,
            cancellationToken);

        if (quote is null)
            return null;

        quote.Accept(
            ownerUserId,
            request.AcceptedByName,
            request.AcceptedByTitle,
            request.SubjectivitiesAcknowledged,
            DateTime.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AcceptQuoteResult(
            quote.Id,
            quote.SubmissionId,
            quote.Status.ToString(),
            quote.Premium,
            quote.RequestedLimit,
            quote.Retention,
            quote.Subjectivities,
            quote.ExpiresAtUtc,
            quote.AcceptedByUserId ?? ownerUserId,
            quote.AcceptedByName ?? request.AcceptedByName,
            quote.AcceptedByTitle ?? request.AcceptedByTitle,
            quote.SubjectivitiesAcknowledged,
            quote.AcceptedAtUtc ?? DateTime.UtcNow);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to accept a quote.")
            : currentUser.UserId;
    }
}
