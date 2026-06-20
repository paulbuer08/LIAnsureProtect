namespace LIAnsureProtect.Infrastructure.Persistence.Idempotency;

public sealed class IdempotencyRecord
{
    private IdempotencyRecord(
        Guid id,
        string key,
        string ownerUserId,
        string actionName,
        string requestFingerprint,
        DateTime createdAtUtc)
    {
        Id = id;
        Key = key;
        OwnerUserId = ownerUserId;
        ActionName = actionName;
        RequestFingerprint = requestFingerprint;
        Status = IdempotencyRecordStatus.InProgress;
        CreatedAtUtc = createdAtUtc;
    }

    private IdempotencyRecord()
    {
        Key = string.Empty;
        OwnerUserId = string.Empty;
        ActionName = string.Empty;
        RequestFingerprint = string.Empty;
        Status = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Key { get; private set; }

    public string OwnerUserId { get; private set; }

    public string ActionName { get; private set; }

    public string RequestFingerprint { get; private set; }

    public string Status { get; private set; }

    public int? ResponseStatusCode { get; private set; }

    public string? ResponseBody { get; private set; }

    public string? ResponseContentType { get; private set; }

    public string? ResponseLocation { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public bool IsCompleted => Status == IdempotencyRecordStatus.Completed;

    public bool Matches(
        string ownerUserId,
        string actionName,
        string requestFingerprint)
    {
        return OwnerUserId == ownerUserId
            && ActionName == actionName
            && RequestFingerprint == requestFingerprint;
    }

    public void MarkCompleted(
        int responseStatusCode,
        string responseBody,
        string responseContentType,
        string? responseLocation,
        DateTime completedAtUtc)
    {
        ResponseStatusCode = responseStatusCode;
        ResponseBody = responseBody;
        ResponseContentType = responseContentType;
        ResponseLocation = responseLocation;
        CompletedAtUtc = completedAtUtc;
        Status = IdempotencyRecordStatus.Completed;
    }

    public static IdempotencyRecord Start(
        string key,
        string ownerUserId,
        string actionName,
        string requestFingerprint,
        DateTime createdAtUtc)
    {
        return new IdempotencyRecord(
            Guid.NewGuid(),
            key,
            ownerUserId,
            actionName,
            requestFingerprint,
            createdAtUtc);
    }
}
