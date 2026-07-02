using MediatR;

namespace LIAnsureProtect.Modules.Quoting.Application.ReferralDecisions;

public sealed record DeclineQuoteReferralCommand(
    Guid QuoteId,
    string Reason,
    string? Notes) : IRequest<UnderwriteQuoteReferralResult?>;
