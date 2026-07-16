using MediatR;

namespace LIAnsureProtect.Modules.Notifications.Application.Commands.AcknowledgeNotificationSubject;

public sealed record AcknowledgeNotificationSubjectCommand(
    string SubjectReferenceType,
    string SubjectReferenceId,
    string Scope) : IRequest;
