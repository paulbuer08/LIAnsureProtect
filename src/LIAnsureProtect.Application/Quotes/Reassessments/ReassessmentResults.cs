using LIAnsureProtect.Domain.Quotes;

namespace LIAnsureProtect.Application.Quotes.Reassessments;

public sealed record ReassessmentRequestResult(
    Guid ReassessmentRequestId,
    Guid SubmissionId,
    Guid BaseQuoteId,
    int BaseQuoteVersion,
    string Status,
    string SubmissionReference,
    string CompanyName,
    DateTime RequestedAtUtc,
    string RequestedByUserId,
    DateTime? ReviewedAtUtc,
    string? ReviewedByUserId,
    string? DecisionReason,
    Guid? CreatedQuoteId);

internal static class ReassessmentRequestResultFactory
{
    public static ReassessmentRequestResult FromRequest(ReassessmentRequest request)
        => new(
            request.Id,
            request.SubmissionId,
            request.BaseQuoteId,
            request.BaseQuoteVersion,
            request.Status.ToString(),
            request.SubmissionReference,
            request.CompanyName,
            request.RequestedAtUtc,
            request.RequestedByUserId,
            request.ReviewedAtUtc,
            request.ReviewedByUserId,
            request.DecisionReason,
            request.CreatedQuoteId);
}
