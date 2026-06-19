using LIAnsureProtect.Application.Common.Security;
using MediatR;

namespace LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;

public sealed class GetSubmissionDetailQueryHandler(
    ISubmissionRepository submissionRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetSubmissionDetailQuery, SubmissionDetailResult?>
{
    public async Task<SubmissionDetailResult?> Handle(
        GetSubmissionDetailQuery request,
        CancellationToken cancellationToken)
    {
        return await submissionRepository.GetDetailAsync(
            request.SubmissionId,
            GetRequiredCurrentUserId(),
            cancellationToken);
    }

    private string GetRequiredCurrentUserId()
    {
        return string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to view a submission.")
            : currentUser.UserId;
    }
}
