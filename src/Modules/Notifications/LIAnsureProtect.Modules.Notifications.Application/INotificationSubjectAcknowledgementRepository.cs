namespace LIAnsureProtect.Modules.Notifications.Application;

public interface INotificationSubjectAcknowledgementRepository
{
    Task AcknowledgeAsync(
        string recipientUserId,
        string scope,
        IReadOnlyCollection<string> audiences,
        string subjectReferenceType,
        string subjectReferenceId,
        DateTime viewedThroughUtc,
        CancellationToken cancellationToken);
}
