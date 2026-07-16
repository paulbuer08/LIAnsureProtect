using LIAnsureProtect.Platform.Abstractions.Security;
using MediatR;

namespace LIAnsureProtect.Modules.Notifications.Application.Commands.AcknowledgeNotificationSubject;

public sealed class AcknowledgeNotificationSubjectCommandHandler(
    INotificationSubjectAcknowledgementRepository repository,
    ICurrentUser currentUser) : IRequestHandler<AcknowledgeNotificationSubjectCommand>
{
    private static readonly HashSet<string> AllowedSubjectTypes = new(StringComparer.Ordinal)
    {
        "evidence-request",
        "quote",
        "policy",
        "claim",
        "submission",
        "reassessment_request"
    };

    public async Task Handle(
        AcknowledgeNotificationSubjectCommand request,
        CancellationToken cancellationToken)
    {
        var recipientUserId = string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new InvalidOperationException("An authenticated user id is required to acknowledge notifications.")
            : currentUser.UserId;
        var subjectType = request.SubjectReferenceType.Trim().ToLowerInvariant();
        var subjectId = request.SubjectReferenceId.Trim();
        var scope = request.Scope.Trim().ToLowerInvariant();

        if (!AllowedSubjectTypes.Contains(subjectType))
            throw new ArgumentException("The notification subject type is not supported.", nameof(request));
        if (subjectId.Length is 0 or > 128)
            throw new ArgumentException("The notification subject id is invalid.", nameof(request));
        if (scope is not NotificationScopes.Personal and not NotificationScopes.Team)
            throw new ArgumentException("The notification scope is invalid.", nameof(request));

        var audiences = scope == NotificationScopes.Personal
            ? new[] { NotificationAudiences.CustomerOrBroker }
            : NotificationTeamAudiences.ForRoles(currentUser.GetRoles());

        if (audiences.Count == 0)
            throw new UnauthorizedAccessException("The current user has no team notification audience.");

        await repository.AcknowledgeAsync(
            recipientUserId,
            scope,
            audiences,
            subjectType,
            subjectId,
            DateTime.UtcNow,
            cancellationToken);
    }
}
