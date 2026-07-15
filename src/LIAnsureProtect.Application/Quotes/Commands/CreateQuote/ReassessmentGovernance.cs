using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LIAnsureProtect.Application.Quotes.Commands.CreateQuote;

public static class ReassessmentGovernancePolicy
{
    public const int MaxSelfServiceInRollingWindow = 2;
    public const int RollingWindowHours = 24;
    public const int MaxSelfServiceLifetime = 5;
    public const int CooldownMinutes = 30;

    public static bool RequiresManualReview(
        int successfulInRollingWindow,
        int successfulLifetime,
        DateTime latestQuoteCreatedAtUtc,
        DateTime nowUtc)
        => successfulInRollingWindow >= MaxSelfServiceInRollingWindow
            || successfulLifetime >= MaxSelfServiceLifetime
            || nowUtc - latestQuoteCreatedAtUtc < TimeSpan.FromMinutes(CooldownMinutes);
}

public sealed record ReassessmentReviewQueuedResult(
    Guid ReassessmentRequestId,
    Guid SubmissionId,
    Guid BaseQuoteId,
    int BaseQuoteVersion,
    string Status,
    DateTime RequestedAtUtc,
    string Message);

public sealed class ReassessmentReviewQueuedException(ReassessmentReviewQueuedResult result) : Exception
{
    public ReassessmentReviewQueuedResult Result { get; } = result;
}

internal static class ReassessmentRequestPayloadSerializer
{
    private static readonly JsonSerializerOptions Options = JsonSerializerOptions.Web;

    public static string Serialize(CreateQuoteCommand command)
        => JsonSerializer.Serialize(command, Options);

    public static CreateQuoteCommand Deserialize(string payload)
        => JsonSerializer.Deserialize<CreateQuoteCommand>(payload, Options)
            ?? throw new InvalidOperationException("The reassessment request payload is invalid.");

    public static string Fingerprint(string payload)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
}
