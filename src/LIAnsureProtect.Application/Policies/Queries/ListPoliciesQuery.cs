using MediatR;

namespace LIAnsureProtect.Application.Policies.Queries;

public sealed record ListPoliciesQuery : IRequest<ListPoliciesResult>;
