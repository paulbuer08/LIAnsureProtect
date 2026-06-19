using MediatR;

namespace LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;

public sealed record GetSubmissionDetailQuery(Guid SubmissionId)
    : IRequest<SubmissionDetailResult?>;
