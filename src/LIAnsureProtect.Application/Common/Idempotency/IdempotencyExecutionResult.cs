namespace LIAnsureProtect.Application.Common.Idempotency;

public sealed record IdempotencyExecutionResult(
    IdempotencyExecutionStatus Status,
    IdempotencyActionResponse? Response,
    string? ConflictDetail)
{
    public static IdempotencyExecutionResult Completed(IdempotencyActionResponse response)
    {
        return new IdempotencyExecutionResult(
            IdempotencyExecutionStatus.Completed,
            response,
            ConflictDetail: null);
    }

    public static IdempotencyExecutionResult Replayed(IdempotencyActionResponse response)
    {
        return new IdempotencyExecutionResult(
            IdempotencyExecutionStatus.Replayed,
            response,
            ConflictDetail: null);
    }

    public static IdempotencyExecutionResult Conflict(string detail)
    {
        return new IdempotencyExecutionResult(
            IdempotencyExecutionStatus.Conflict,
            Response: null,
            detail);
    }
}
