using MediatR;

namespace LIAnsureProtect.Application.Policies.Queries;

public sealed record GetPolicyQuery(Guid PolicyId) : IRequest<PolicyResult?>;
