using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.UnderwriteQuoteReferral;

public sealed record DeclineQuoteReferralCommand(
    Guid QuoteId,
    string Reason,
    string? Notes) : IRequest<UnderwriteQuoteReferralResult?>;
