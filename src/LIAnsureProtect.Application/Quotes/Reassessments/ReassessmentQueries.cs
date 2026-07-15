using LIAnsureProtect.Domain.Quotes;
using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Application.Quotes.Reassessments;

public sealed record ListOwnedReassessmentRequestsQuery(Guid SubmissionId)
    : IRequest<IReadOnlyCollection<ReassessmentRequestResult>?>;

public sealed record ListReassessmentRequestsForReviewQuery(string? Status = "Pending")
    : IRequest<IReadOnlyCollection<ReassessmentRequestResult>>;

public sealed class ListOwnedReassessmentRequestsQueryHandler(
    IReassessmentRequestRepository repository,
    ISubmissionRepository submissions,
    ICurrentUser currentUser)
    : IRequestHandler<ListOwnedReassessmentRequestsQuery, IReadOnlyCollection<ReassessmentRequestResult>?>
{
    public async Task<IReadOnlyCollection<ReassessmentRequestResult>?> Handle(
        ListOwnedReassessmentRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = RequiredUser(currentUser);
        if (await submissions.GetDetailAsync(request.SubmissionId, ownerUserId, cancellationToken) is null)
            return null;

        var items = await repository.ListOwnedAsync(request.SubmissionId, ownerUserId, cancellationToken);
        return items.Select(ReassessmentRequestResultFactory.FromRequest).ToList();
    }

    private static string RequiredUser(ICurrentUser user)
        => string.IsNullOrWhiteSpace(user.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required.")
            : user.UserId;
}

public sealed class ListReassessmentRequestsForReviewQueryHandler(
    IReassessmentRequestRepository repository)
    : IRequestHandler<ListReassessmentRequestsForReviewQuery, IReadOnlyCollection<ReassessmentRequestResult>>
{
    public async Task<IReadOnlyCollection<ReassessmentRequestResult>> Handle(
        ListReassessmentRequestsForReviewQuery request,
        CancellationToken cancellationToken)
    {
        ReassessmentRequestStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<ReassessmentRequestStatus>(request.Status, true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                throw new ArgumentException("Reassessment request status is invalid.", nameof(request));
            }

            status = parsed;
        }

        var items = await repository.ListForReviewAsync(status, cancellationToken);
        return items.Select(ReassessmentRequestResultFactory.FromRequest).ToList();
    }
}
