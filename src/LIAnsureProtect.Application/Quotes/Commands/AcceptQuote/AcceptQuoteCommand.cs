using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.AcceptQuote;

public sealed record AcceptQuoteCommand(
    Guid QuoteId,
    string AcceptedByName,
    string AcceptedByTitle,
    bool SubjectivitiesAcknowledged) : IRequest<AcceptQuoteResult?>;
