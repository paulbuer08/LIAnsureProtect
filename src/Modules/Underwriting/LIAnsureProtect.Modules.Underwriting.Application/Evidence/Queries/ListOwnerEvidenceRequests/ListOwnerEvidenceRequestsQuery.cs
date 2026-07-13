using System.Globalization;
using System.Text;
using LIAnsureProtect.Modules.Underwriting.Domain.Evidence;
using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Underwriting.Application.Evidence.Queries.ListOwnerEvidenceRequests;

public sealed record ListOwnerEvidenceRequestsQuery(
    string? Status = null,
    string? Category = null,
    Guid? QuoteId = null,
    bool? Overdue = null,
    string? Cursor = null,
    int PageSize = 12) : IRequest<ListOwnerEvidenceRequestsResult>;

public sealed class ListOwnerEvidenceRequestsQueryHandler(
    IEvidenceRequestsReader reader,
    ICurrentUser currentUser)
    : IRequestHandler<ListOwnerEvidenceRequestsQuery, ListOwnerEvidenceRequestsResult>
{
    public async Task<ListOwnerEvidenceRequestsResult> Handle(
        ListOwnerEvidenceRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = CurrentEvidenceUser.GetRequiredUserId(
            currentUser,
            "An authenticated owner user id is required to list evidence requests.");
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var cursor = DecodeCursor(request.Cursor);
        var status = ParseFilter<EvidenceRequestStatus>(request.Status, nameof(request.Status));
        var category = ParseFilter<EvidenceRequestCategory>(request.Category, nameof(request.Category));
        var evidenceRequests = await reader.GetOwnerRequestsPageAsync(
            ownerUserId,
            status,
            category,
            request.QuoteId,
            request.Overdue,
            cursor?.DueAtUtc,
            cursor?.RequestedAtUtc,
            cursor?.EvidenceRequestId,
            pageSize + 1,
            cancellationToken);
        var page = evidenceRequests.Take(pageSize).ToList();
        var nextCursor = evidenceRequests.Count > pageSize && page.Count > 0
            ? EncodeCursor(
                page[^1].DueAtUtc,
                page[^1].RequestedAtUtc,
                page[^1].EvidenceRequestId)
            : null;

        return new ListOwnerEvidenceRequestsResult(
            page.Select(item => new EvidenceRequestOwnerSummaryResult(
                item.EvidenceRequestId,
                item.QuoteId,
                item.SubmissionId,
                item.Category.ToString(),
                item.Title,
                item.Description,
                item.DueAtUtc,
                item.Status.ToString(),
                item.IsOverdue,
                item.DaysUntilDue,
                item.RequestedAtUtc,
                item.ReviewDecision.ToString(),
                item.RemediationGuidance,
                item.UpdatedAtUtc)).ToList(),
            nextCursor);
    }

    private static TEnum? ParseFilter<TEnum>(string? value, string parameterName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Enum.TryParse<TEnum>(value.Trim(), true, out var parsed)
            && Enum.IsDefined(parsed))
            return parsed;

        throw new ArgumentException($"{parameterName} is invalid.", parameterName);
    }

    private static string EncodeCursor(
        DateTime dueAtUtc,
        DateTime requestedAtUtc,
        Guid evidenceRequestId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{dueAtUtc.Ticks.ToString(CultureInfo.InvariantCulture)}:{requestedAtUtc.Ticks.ToString(CultureInfo.InvariantCulture)}:{evidenceRequestId:N}"));

    private static EvidenceCursor? DecodeCursor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split(':');
            if (parts.Length != 3
                || !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var dueTicks)
                || !long.TryParse(parts[1], CultureInfo.InvariantCulture, out var requestedTicks)
                || !Guid.TryParseExact(parts[2], "N", out var evidenceRequestId))
                throw new FormatException();

            return new EvidenceCursor(
                new DateTime(dueTicks, DateTimeKind.Utc),
                new DateTime(requestedTicks, DateTimeKind.Utc),
                evidenceRequestId);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            throw new ArgumentException("Evidence request cursor is invalid.", nameof(value));
        }
    }

    private sealed record EvidenceCursor(
        DateTime DueAtUtc,
        DateTime RequestedAtUtc,
        Guid EvidenceRequestId);
}
