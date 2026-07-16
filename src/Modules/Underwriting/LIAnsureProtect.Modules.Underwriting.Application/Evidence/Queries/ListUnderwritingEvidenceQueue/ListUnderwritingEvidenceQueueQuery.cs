using System.Globalization;
using System.Text;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using MediatR;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.ListUnderwritingEvidenceQueue;

public sealed record ListUnderwritingEvidenceQueueQuery(
    string? Search = null,
    string? Status = null,
    string? ReviewDecision = null,
    bool? Overdue = null,
    bool? UnreadFollowUps = null,
    string? Cursor = null,
    int PageSize = 12) : IRequest<ListUnderwritingEvidenceQueueResult>;

public sealed class ListUnderwritingEvidenceQueueQueryHandler(IEvidenceRequestsReader reader)
    : IRequestHandler<ListUnderwritingEvidenceQueueQuery, ListUnderwritingEvidenceQueueResult>
{
    public async Task<ListUnderwritingEvidenceQueueResult> Handle(
        ListUnderwritingEvidenceQueueQuery request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Search) && request.Search.Trim().Length > 200)
            throw new ArgumentException("Search text must not exceed 200 characters.", nameof(request));

        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var cursor = DecodeCursor(request.Cursor);
        var items = await reader.GetUnderwritingQueuePageAsync(
            request.Search?.Trim(),
            ParseFilter<EvidenceRequestStatus>(request.Status, nameof(request.Status)),
            ParseFilter<EvidenceReviewDecisionStatus>(request.ReviewDecision, nameof(request.ReviewDecision)),
            request.Overdue,
            request.UnreadFollowUps,
            cursor?.Priority,
            cursor?.UpdatedAtUtc,
            cursor?.EvidenceRequestId,
            pageSize + 1,
            cancellationToken);
        var page = items.Take(pageSize).ToList();
        var nextCursor = items.Count > pageSize && page.Count > 0
            ? EncodeCursor(page[^1].Priority, page[^1].UpdatedAtUtc, page[^1].EvidenceRequestId)
            : null;

        return new ListUnderwritingEvidenceQueueResult(
            page.Select(item => new UnderwritingEvidenceQueueItemResult(
                item.EvidenceRequestId,
                item.QuoteId,
                item.SubmissionId,
                item.SubmissionReference,
                item.CompanyName,
                item.QuoteVersion,
                item.Category.ToString(),
                item.Title,
                item.DueAtUtc,
                item.Status.ToString(),
                item.ReviewDecision.ToString(),
                item.DocumentRequirement.ToString(),
                item.PendingFollowUpCount,
                item.DocumentCount,
                item.DownloadableDocumentCount,
                item.OldestPendingFollowUpAtUtc,
                item.UpdatedAtUtc,
                item.IsOverdue)).ToList(),
            nextCursor);
    }

    private static TEnum? ParseFilter<TEnum>(string? value, string parameterName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Enum.TryParse<TEnum>(value.Trim(), true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
        throw new ArgumentException($"{parameterName} is invalid.", parameterName);
    }

    private static string EncodeCursor(int priority, DateTime updatedAtUtc, Guid evidenceRequestId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{priority.ToString(CultureInfo.InvariantCulture)}:{updatedAtUtc.Ticks.ToString(CultureInfo.InvariantCulture)}:{evidenceRequestId:N}"));

    private static EvidenceQueueCursor? DecodeCursor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split(':');
            if (parts.Length != 3
                || !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var priority)
                || !long.TryParse(parts[1], CultureInfo.InvariantCulture, out var updatedTicks)
                || !Guid.TryParseExact(parts[2], "N", out var evidenceRequestId))
                throw new FormatException();
            return new EvidenceQueueCursor(priority, new DateTime(updatedTicks, DateTimeKind.Utc), evidenceRequestId);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            throw new ArgumentException("Evidence queue cursor is invalid.", nameof(value));
        }
    }

    private sealed record EvidenceQueueCursor(int Priority, DateTime UpdatedAtUtc, Guid EvidenceRequestId);
}
