namespace LIAnsureProtect.Application.Common.Idempotency;

public sealed record IdempotencyRequest(
    string Key,
    string OwnerUserId,
    string ActionName,
    string RequestFingerprint);
