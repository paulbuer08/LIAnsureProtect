namespace LIAnsureProtect.Application.Common.Idempotency;

public enum IdempotencyExecutionStatus
{
    Completed,
    Replayed,
    Conflict
}
