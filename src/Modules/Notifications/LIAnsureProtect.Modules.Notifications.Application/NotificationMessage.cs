namespace LIAnsureProtect.Modules.Notifications.Application;

public sealed record NotificationMessage(
    string MessageId,
    Guid OutboxMessageId,
    string Type,
    string Audience,
    string OwnerUserId,
    string SubjectReferenceType,
    string SubjectReferenceId,
    DateTime OccurredAtUtc,
    IReadOnlyDictionary<string, string> Attributes);
