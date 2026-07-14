using MediatR;

namespace LIAnsureProtect.Application.Policies.Queries;

public sealed record ListPoliciesQuery(
    string? Search = null,
    string? ContractualStatus = null,
    string? CoverageState = null) : IRequest<ListPoliciesResult>;
