using MediatR;

namespace LIAnsureProtect.Application.Quotes.Commands.UnderwriteQuoteReferral;

public sealed record AdjustQuoteReferralCommand(
    Guid QuoteId,
    decimal AdjustedPremium,
    decimal AdjustedRetention,
    string Reason,
    string? Notes,
    string? UpdatedSubjectivities) : IRequest<UnderwriteQuoteReferralResult?>;
