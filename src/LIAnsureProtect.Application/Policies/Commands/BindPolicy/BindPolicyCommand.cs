using MediatR;

namespace LIAnsureProtect.Application.Policies.Commands.BindPolicy;

public sealed record BindPolicyCommand(
    Guid QuoteId,
    DateTime EffectiveDateUtc) : IRequest<BindPolicyResult?>;
