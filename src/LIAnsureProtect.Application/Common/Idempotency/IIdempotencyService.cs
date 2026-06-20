namespace LIAnsureProtect.Application.Common.Idempotency;

public interface IIdempotencyService
{
    Task<IdempotencyExecutionResult> ExecuteAsync(
        IdempotencyRequest request,
        Func<CancellationToken, Task<IdempotencyActionResponse>> operation,
        CancellationToken cancellationToken);
}
