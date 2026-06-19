using MediatR;

namespace LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;

public sealed class GetSubmissionDetailQueryHandler(ISubmissionRepository submissionRepository)
    : IRequestHandler<GetSubmissionDetailQuery, SubmissionDetailResult?>
{
    public async Task<SubmissionDetailResult?> Handle(
        GetSubmissionDetailQuery request,
        CancellationToken cancellationToken)
    {
        return await submissionRepository.GetDetailAsync(
            request.SubmissionId,
            cancellationToken);
    }
}
