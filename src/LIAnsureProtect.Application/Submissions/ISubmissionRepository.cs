using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Application.Submissions.Queries.GetSubmissionDetail;
using LIAnsureProtect.Application.Submissions.Queries.ListSubmissions;

namespace LIAnsureProtect.Application.Submissions;

public interface ISubmissionRepository
{
    Task AddAsync(Submission submission, CancellationToken cancellationToken);

    Task<ListSubmissionsResult> ListAsync(
        string ownerUserId,
        SubmissionListFilter filter,
        CancellationToken cancellationToken);

    Task<SubmissionDetailResult?> GetDetailAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<Submission?> GetOwnedForUpdateAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken);

    void Remove(Submission submission);

    Task<bool> HasAcceptedOrBoundQuoteAsync(
        Guid submissionId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<bool> HasOpenSubmissionForCompanyAsync(
        string ownerUserId,
        string companyName,
        CancellationToken cancellationToken);

    Task<Guid?> FindMatchingDraftIdAsync(
        string ownerUserId,
        string applicantName,
        string applicantEmail,
        string companyName,
        CancellationToken cancellationToken);
}
