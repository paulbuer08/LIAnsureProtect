using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.UnderwriteQuoteReferral;

public sealed record ApproveQuoteReferralCommand(
    Guid QuoteId,
    string Reason,
    string? Notes) : IRequest<UnderwriteQuoteReferralResult?>;
