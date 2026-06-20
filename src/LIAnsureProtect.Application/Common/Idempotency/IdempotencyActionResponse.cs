using System.Text.Json;

namespace LIAnsureProtect.Application.Common.Idempotency;

public sealed record IdempotencyActionResponse(
    int StatusCode,
    string Body,
    string ContentType,
    string? Location)
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    public static IdempotencyActionResponse Json<T>(
        int statusCode,
        T body,
        string? location = null)
    {
        return new IdempotencyActionResponse(
            statusCode,
            JsonSerializer.Serialize(body, JsonOptions),
            "application/json",
            location);
    }
}
